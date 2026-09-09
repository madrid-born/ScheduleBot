using Microsoft.EntityFrameworkCore;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

/// <summary>
/// Read-only Tehran Metro data access and route/timetable calculations.
/// This class deliberately has no Telegram dependencies.
/// </summary>
public sealed class MetroService(MetroDbContext dbContext, IConfiguration configuration)
{
    private readonly double _walkingSpeedMetersPerMinute =
        configuration.GetValue("Metro:WalkingSpeedMetersPerMinute", 80d);
    private readonly double _walkingDistanceFactor =
        configuration.GetValue("Metro:WalkingDistanceFactor", 1.25d);
    private readonly double _trainSpeedKmPerHour =
        configuration.GetValue("Metro:FallbackTrainSpeedKmPerHour", 35d);
    private readonly int _defaultTransferWalkSeconds =
        configuration.GetValue("Metro:DefaultTransferWalkSeconds", 240);

    public async Task<IReadOnlyList<MetroLineDetails>> GetOperationalLinesAsync(
        CancellationToken cancellationToken = default)
    {
        var stationCounts = await dbContext.StationLines
            .AsNoTracking()
            .Where(x => x.IsOperational)
            .GroupBy(x => x.LineId)
            .Select(group => new { LineId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.LineId, x => x.Count, cancellationToken);
        var lines = await dbContext.Lines
            .AsNoTracking()
            .OrderBy(x => x.LineId)
            .ToListAsync(cancellationToken);
        return lines
            .Where(x => stationCounts.ContainsKey(x.LineId))
            .Select(x => new MetroLineDetails(
                x.LineId,
                x.NameFa,
                x.NameEn,
                x.Color,
                stationCounts[x.LineId]))
            .ToList();
    }

    public async Task<IReadOnlyList<MetroStationDetails>> GetStationsByLineAsync(
        byte lineId,
        CancellationToken cancellationToken = default)
    {
        var stations = await (
            from link in dbContext.StationLines.AsNoTracking()
            join station in dbContext.Stations.AsNoTracking() on link.StationId equals station.StationId
            where link.LineId == lineId &&
                  link.IsOperational &&
                  station.InfrastructureStatus == "operational"
            orderby station.NameEn
            select station)
            .ToListAsync(cancellationToken);

        var stationIds = stations.Select(x => x.StationId).ToList();
        var lineLinks = await dbContext.StationLines.AsNoTracking()
            .Where(x => stationIds.Contains(x.StationId) && x.IsOperational)
            .ToListAsync(cancellationToken);
        return stations
            .Select(x => ToDetails(x, lineLinks.Where(link => link.StationId == x.StationId).Select(link => link.LineId)))
            .ToList();
    }

    public async Task<IReadOnlyList<MetroStationDetails>> SearchStationsAsync(
        string query,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        var stations = await dbContext.Stations
            .AsNoTracking()
            .Where(x => x.InfrastructureStatus == "operational" &&
                        (x.NameEn.Contains(query) || x.NameFa.Contains(query)))
            .OrderBy(x => x.NameEn)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var stationIds = stations.Select(x => x.StationId).ToList();
        var lines = await dbContext.StationLines
            .AsNoTracking()
            .Where(x => stationIds.Contains(x.StationId) && x.IsOperational)
            .ToListAsync(cancellationToken);

        return stations
            .Select(x => ToDetails(x, lines.Where(l => l.StationId == x.StationId).Select(l => l.LineId)))
            .ToList();
    }

    public async Task<MetroStationDetails?> GetStationDetailsAsync(
        string stationId,
        CancellationToken cancellationToken = default)
    {
        var station = await dbContext.Stations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StationId == stationId, cancellationToken);
        if (station == null) return null;

        var lines = await dbContext.StationLines.AsNoTracking()
            .Where(x => x.StationId == stationId && x.IsOperational)
            .OrderBy(x => x.LineId)
            .Select(x => x.LineId)
            .ToListAsync(cancellationToken);
        return ToDetails(station, lines);
    }

    public async Task<MetroNavigationResult?> NavigateAsync(
        MetroGeoPoint origin,
        MetroGeoPoint destination,
        DateTime? requestedAtTehran = null,
        CancellationToken cancellationToken = default)
    {
        var stations = await dbContext.Stations.AsNoTracking().ToListAsync(cancellationToken);
        var stationLines = await dbContext.StationLines.AsNoTracking()
            .Where(x => x.IsOperational)
            .ToListAsync(cancellationToken);
        var boardableIds = stationLines.Select(x => x.StationId).ToHashSet(StringComparer.Ordinal);
        var boardableStations = stations
            .Where(x => x.InfrastructureStatus == "operational" && boardableIds.Contains(x.StationId))
            .ToList();
        if (boardableStations.Count == 0) return null;

        var originStation = FindNearest(origin, boardableStations);
        var destinationStation = FindNearest(destination, boardableStations);
        var stationById = stations.ToDictionary(x => x.StationId, StringComparer.Ordinal);

        var originWalkMeters = originStation.DistanceMeters * _walkingDistanceFactor;
        var destinationWalkMeters = destinationStation.DistanceMeters * _walkingDistanceFactor;
        var originWalk = WalkingTime(originWalkMeters);
        var destinationWalk = WalkingTime(destinationWalkMeters);
        var now = requestedAtTehran ?? GetTehranNow();

        if (originStation.StationId == destinationStation.StationId)
        {
            var arrival = now + originWalk + destinationWalk;
            return new MetroNavigationResult(
                originStation, destinationStation,
                originWalkMeters, originWalk,
                destinationWalkMeters, destinationWalk,
                [], now, arrival, arrival - now);
        }

        var segments = await dbContext.Segments.AsNoTracking()
            .Where(x => x.SegmentStatus == "operational")
            .ToListAsync(cancellationToken);
        var routeStops = await dbContext.RouteStops.AsNoTracking()
            .Where(x => x.StopStatus == "operational")
            .Select(x => new { x.RouteId, x.StationId })
            .ToListAsync(cancellationToken);
        var operationalRouteStops = routeStops
            .Select(x => (x.RouteId, x.StationId))
            .ToHashSet();
        var operationalLineStops = stationLines
            .Select(x => (x.StationId, x.LineId))
            .ToHashSet();

        var path = FindPath(
            originStation.StationId,
            destinationStation.StationId,
            stationById,
            segments,
            operationalRouteStops,
            operationalLineStops);
        if (path.Count == 0) return null;

        var transferRules = await dbContext.TransferRules.AsNoTracking().ToListAsync(cancellationToken);
        var lineById = await dbContext.Lines.AsNoTracking()
            .ToDictionaryAsync(x => x.LineId, cancellationToken);
        var plannedLegs = GroupPath(path);
        var resultLegs = new List<MetroJourneyLeg>();
        var readyAt = now + originWalk;

        for (var index = 0; index < plannedLegs.Count; index++)
        {
            var planned = plannedLegs[index];
            var transferSeconds = 0;
            if (index > 0)
            {
                var previous = plannedLegs[index - 1];
                transferSeconds = previous.LineId == planned.LineId
                    ? 0
                    : transferRules.FirstOrDefault(x =>
                        x.StationId == planned.FromStationId &&
                        x.FromLineId == previous.LineId &&
                        x.ToLineId == planned.LineId)?.WalkSeconds ?? _defaultTransferWalkSeconds;
                readyAt = readyAt.AddSeconds(transferSeconds);
            }

            var scheduled = await FindNextTripAsync(planned, readyAt, cancellationToken);
            var waitTime = TimeSpan.Zero;
            var serviceDayType = await GetDayTypeAsync(readyAt.Date, cancellationToken);
            DateTime? departure = null;
            DateTime? arrival = null;
            string directionName;
            bool isExpress;
            TimeSpan rideTime;
            bool scheduleFound;

            if (scheduled != null)
            {
                departure = scheduled.Departure;
                arrival = scheduled.Arrival;
                directionName = stationById[scheduled.DirectionStationId].NameEn;
                isExpress = scheduled.IsExpress;
                serviceDayType = scheduled.DayType;
                waitTime = scheduled.Departure - readyAt;
                rideTime = scheduled.Arrival - scheduled.Departure;
                scheduleFound = true;
                readyAt = scheduled.Arrival;
            }
            else
            {
                var distance = planned.Edges.Sum(x =>
                    HaversineMeters(
                        (double)stationById[x.FromStationId].Latitude,
                        (double)stationById[x.FromStationId].Longitude,
                        (double)stationById[x.ToStationId].Latitude,
                        (double)stationById[x.ToStationId].Longitude));
                rideTime = TimeSpan.FromMinutes(Math.Max(1, distance / (_trainSpeedKmPerHour * 1000 / 60)));
                directionName = stationById[planned.ToStationId].NameEn;
                isExpress = false;
                scheduleFound = false;
                readyAt += rideTime;
            }

            resultLegs.Add(new MetroJourneyLeg(
                planned.LineId,
                lineById[planned.LineId].NameEn,
                lineById[planned.LineId].Color,
                planned.RouteId,
                planned.FromStationId,
                stationById[planned.FromStationId].NameEn,
                planned.ToStationId,
                stationById[planned.ToStationId].NameEn,
                directionName,
                isExpress,
                index > 0,
                transferSeconds,
                departure,
                arrival,
                waitTime,
                rideTime,
                serviceDayType,
                scheduleFound));
        }

        var estimatedArrival = readyAt + destinationWalk;
        return new MetroNavigationResult(
            originStation, destinationStation,
            originWalkMeters, originWalk,
            destinationWalkMeters, destinationWalk,
            resultLegs, now, estimatedArrival, estimatedArrival - now);
    }

    private async Task<ScheduledLeg?> FindNextTripAsync(
        PlannedLeg leg,
        DateTime readyAt,
        CancellationToken cancellationToken)
    {
        for (var dayIncrement = 0; dayIncrement <= 1; dayIncrement++)
        {
            var serviceDate = readyAt.Date.AddDays(dayIncrement);
            var dayType = await GetDayTypeAsync(serviceDate, cancellationToken);
            var candidates = await (
                from trip in dbContext.Trips.AsNoTracking()
                join fromStop in dbContext.StopTimes.AsNoTracking() on trip.TripId equals fromStop.TripId
                join toStop in dbContext.StopTimes.AsNoTracking() on trip.TripId equals toStop.TripId
                where trip.LineId == leg.LineId &&
                      trip.RouteId == leg.RouteId &&
                      trip.DayType == dayType &&
                      fromStop.StationId == leg.FromStationId &&
                      toStop.StationId == leg.ToStationId &&
                      fromStop.StopSequence < toStop.StopSequence
                select new
                {
                    trip.DirectionStationId,
                    trip.IsExpress,
                    FromTime = fromStop.ArrivalTime,
                    FromOffset = fromStop.ServiceDayOffset,
                    ToTime = toStop.ArrivalTime,
                    ToOffset = toStop.ServiceDayOffset
                })
                .ToListAsync(cancellationToken);

            var next = candidates
                .Select(x => new ScheduledLeg(
                    serviceDate.Add(x.FromTime).AddDays(x.FromOffset),
                    serviceDate.Add(x.ToTime).AddDays(x.ToOffset),
                    x.DirectionStationId,
                    x.IsExpress,
                    dayType))
                .Where(x => x.Departure >= readyAt)
                .OrderBy(x => x.Arrival)
                .FirstOrDefault();
            if (next != null) return next;
        }
        return null;
    }

    private async Task<string> GetDayTypeAsync(DateTime serviceDate, CancellationToken cancellationToken)
    {
        if (serviceDate.DayOfWeek == DayOfWeek.Friday) return "friday";
        if (await dbContext.HolidayDates.AsNoTracking().AnyAsync(
                x => x.ServiceDate == serviceDate.Date && x.IsOfficialHoliday,
                cancellationToken)) return "friday";
        return serviceDate.DayOfWeek == DayOfWeek.Thursday ? "thursday" : "saturday_wednesday";
    }

    private List<PathEdge> FindPath(
        string origin,
        string destination,
        IReadOnlyDictionary<string, MetroStation> stations,
        IReadOnlyCollection<MetroSegment> segments,
        HashSet<(string RouteId, string StationId)> operationalRouteStops,
        HashSet<(string StationId, byte LineId)> operationalLineStops)
    {
        var adjacency = new Dictionary<string, List<PathEdge>>(StringComparer.Ordinal);
        foreach (var segment in segments)
        {
            AddEdge(new PathEdge(segment.FromStationId, segment.ToStationId, segment.LineId, segment.RouteId));
            AddEdge(new PathEdge(segment.ToStationId, segment.FromStationId, segment.LineId, segment.RouteId));
        }

        var start = new RouteState(origin, 0, string.Empty);
        var queue = new PriorityQueue<RouteState, double>();
        var distance = new Dictionary<RouteState, double> { [start] = 0 };
        var previous = new Dictionary<RouteState, (RouteState State, PathEdge Edge)>();
        queue.Enqueue(start, 0);
        RouteState? goal = null;

        while (queue.TryDequeue(out var state, out var cost))
        {
            if (cost > distance[state]) continue;
            if (state.StationId == destination && state.LineId != 0 &&
                operationalRouteStops.Contains((state.RouteId, destination)))
            {
                goal = state;
                break;
            }
            if (!adjacency.TryGetValue(state.StationId, out var edges)) continue;

            foreach (var edge in edges)
            {
                var changing = state.LineId != 0 &&
                               (state.LineId != edge.LineId || state.RouteId != edge.RouteId);
                if (state.LineId == 0 && !operationalRouteStops.Contains((edge.RouteId, origin))) continue;
                if (changing)
                {
                    if (!operationalRouteStops.Contains((edge.RouteId, state.StationId))) continue;
                    if (!operationalLineStops.Contains((state.StationId, state.LineId)) ||
                        !operationalLineStops.Contains((state.StationId, edge.LineId))) continue;
                }

                var rideSeconds = Math.Max(60, HaversineMeters(
                    (double)stations[edge.FromStationId].Latitude,
                    (double)stations[edge.FromStationId].Longitude,
                    (double)stations[edge.ToStationId].Latitude,
                    (double)stations[edge.ToStationId].Longitude) /
                    (_trainSpeedKmPerHour * 1000 / 3600));
                var changePenalty = !changing ? 0 : state.LineId == edge.LineId ? 120 : 480;
                var next = new RouteState(edge.ToStationId, edge.LineId, edge.RouteId);
                var nextCost = cost + rideSeconds + changePenalty;
                if (distance.TryGetValue(next, out var known) && known <= nextCost) continue;
                distance[next] = nextCost;
                previous[next] = (state, edge);
                queue.Enqueue(next, nextCost);
            }
        }

        if (goal == null) return [];
        var path = new List<PathEdge>();
        var cursor = goal;
        while (cursor != start)
        {
            var step = previous[cursor];
            path.Add(step.Edge);
            cursor = step.State;
        }
        path.Reverse();
        return path;

        void AddEdge(PathEdge edge)
        {
            if (!adjacency.TryGetValue(edge.FromStationId, out var list))
                adjacency[edge.FromStationId] = list = [];
            list.Add(edge);
        }
    }

    private static List<PlannedLeg> GroupPath(IReadOnlyList<PathEdge> path)
    {
        var legs = new List<PlannedLeg>();
        foreach (var edge in path)
        {
            if (legs.Count == 0 || legs[^1].LineId != edge.LineId || legs[^1].RouteId != edge.RouteId)
                legs.Add(new PlannedLeg(edge.LineId, edge.RouteId, edge.FromStationId, edge.ToStationId, [edge]));
            else
            {
                legs[^1].ToStationId = edge.ToStationId;
                legs[^1].Edges.Add(edge);
            }
        }
        return legs;
    }

    private static MetroNearestStation FindNearest(MetroGeoPoint point, IEnumerable<MetroStation> stations)
    {
        return stations
            .Select(x => new MetroNearestStation(
                x.StationId, x.NameFa, x.NameEn,
                (double)x.Latitude, (double)x.Longitude,
                HaversineMeters(point.Latitude, point.Longitude, (double)x.Latitude, (double)x.Longitude)))
            .MinBy(x => x.DistanceMeters)!;
    }

    private TimeSpan WalkingTime(double meters) =>
        TimeSpan.FromMinutes(Math.Max(1, Math.Ceiling(meters / _walkingSpeedMetersPerMinute)));

    private static double HaversineMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadius = 6_371_000;
        static double Radians(double value) => value * Math.PI / 180;
        var lat1 = Radians(latitude1);
        var lat2 = Radians(latitude2);
        var deltaLat = Radians(latitude2 - latitude1);
        var deltaLon = Radians(longitude2 - longitude1);
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static MetroStationDetails ToDetails(MetroStation station, IEnumerable<byte> lines)
    {
        var amenities = new List<string>();
        if (station.HasWc) amenities.Add("WC");
        if (station.HasElevator) amenities.Add("Elevator");
        if (station.HasAtm) amenities.Add("ATM");
        if (station.HasCoffeeShop) amenities.Add("Coffee shop");
        if (station.HasFastFood) amenities.Add("Fast food");
        if (station.HasGroceryStore) amenities.Add("Grocery store");
        if (station.HasFreeWifi) amenities.Add("Free Wi-Fi");
        if (station.HasPrayerRoom) amenities.Add("Prayer room");
        if (station.HasParking) amenities.Add("Parking");
        if (station.HasPolice) amenities.Add("Police");
        return new MetroStationDetails(
            station.StationId, station.NameFa, station.NameEn,
            (double)station.Latitude, (double)station.Longitude,
            lines.OrderBy(x => x).ToList(), station.InfrastructureStatus, amenities);
    }

    private static DateTime GetTehranNow()
    {
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
        catch (TimeZoneNotFoundException) { timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    }

    private sealed record RouteState(string StationId, byte LineId, string RouteId);
    private sealed record PathEdge(string FromStationId, string ToStationId, byte LineId, string RouteId);
    private sealed class PlannedLeg(
        byte lineId,
        string routeId,
        string fromStationId,
        string toStationId,
        List<PathEdge> edges)
    {
        public byte LineId { get; } = lineId;
        public string RouteId { get; } = routeId;
        public string FromStationId { get; } = fromStationId;
        public string ToStationId { get; set; } = toStationId;
        public List<PathEdge> Edges { get; } = edges;
    }
    private sealed record ScheduledLeg(
        DateTime Departure,
        DateTime Arrival,
        string DirectionStationId,
        bool IsExpress,
        string DayType);
}
