namespace ScheduleBot.Models;

public sealed class MetroStation
{
    public string StationId { get; set; } = null!;
    public string NameFa { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool HasWc { get; set; }
    public bool HasElevator { get; set; }
    public bool HasAtm { get; set; }
    public bool HasCoffeeShop { get; set; }
    public bool HasFastFood { get; set; }
    public bool HasGroceryStore { get; set; }
    public bool HasFreeWifi { get; set; }
    public bool HasPrayerRoom { get; set; }
    public bool HasParking { get; set; }
    public bool HasPolice { get; set; }
    public bool AmenitiesVerified { get; set; }
    public string InfrastructureStatus { get; set; } = null!;
}

public sealed class MetroLine
{
    public byte LineId { get; set; }
    public string NameFa { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string Color { get; set; } = null!;
}

public sealed class MetroStationLine
{
    public string StationId { get; set; } = null!;
    public byte LineId { get; set; }
    public bool IsOperational { get; set; }
}

public sealed class MetroRoute
{
    public string RouteId { get; set; } = null!;
    public byte LineId { get; set; }
    public string BranchId { get; set; } = null!;
    public bool IsBranch { get; set; }
}

public sealed class MetroRouteStop
{
    public string RouteId { get; set; } = null!;
    public short StopSequence { get; set; }
    public string StationId { get; set; } = null!;
    public string StopStatus { get; set; } = null!;
}

public sealed class MetroSegment
{
    public string SegmentId { get; set; } = null!;
    public byte LineId { get; set; }
    public string RouteId { get; set; } = null!;
    public string BranchId { get; set; } = null!;
    public string FromStationId { get; set; } = null!;
    public string ToStationId { get; set; } = null!;
    public string SegmentStatus { get; set; } = null!;
}

public sealed class MetroTransferRule
{
    public short TransferRuleId { get; set; }
    public string StationId { get; set; } = null!;
    public byte FromLineId { get; set; }
    public byte ToLineId { get; set; }
    public short WalkSeconds { get; set; }
    public string Confidence { get; set; } = null!;
    public string FromRouteId { get; set; } = null!;
    public string ToRouteId { get; set; } = null!;
}

public sealed class MetroTrip
{
    public long TripId { get; set; }
    public short ScheduleGroupId { get; set; }
    public byte LineId { get; set; }
    public string RouteId { get; set; } = null!;
    public string DirectionStationId { get; set; } = null!;
    public string DayType { get; set; } = null!;
    public bool IsExpress { get; set; }
    public string OriginStationId { get; set; } = null!;
    public string DestinationStationId { get; set; } = null!;
    public TimeSpan DepartureTime { get; set; }
    public byte DepartureDayOffset { get; set; }
    public TimeSpan ArrivalTime { get; set; }
    public byte ArrivalDayOffset { get; set; }
    public int SourceTripOrdinal { get; set; }
}

public sealed class MetroStopTime
{
    public long TripId { get; set; }
    public short StopSequence { get; set; }
    public string StationId { get; set; } = null!;
    public TimeSpan ArrivalTime { get; set; }
    public byte ServiceDayOffset { get; set; }
    public short MinuteOfDay { get; set; }
}

public sealed class MetroHolidayDate
{
    public DateTime ServiceDate { get; set; }
    public string JalaliDate { get; set; } = null!;
    public bool IsOfficialHoliday { get; set; }
    public string Source { get; set; } = null!;
    public DateTime FetchedAtUtc { get; set; }
}

public sealed record MetroGeoPoint(double Latitude, double Longitude);

public sealed record MetroNearestStation(
    string StationId,
    string NameFa,
    string NameEn,
    double Latitude,
    double Longitude,
    double DistanceMeters);

public sealed record MetroStationDetails(
    string StationId,
    string NameFa,
    string NameEn,
    double Latitude,
    double Longitude,
    IReadOnlyList<byte> Lines,
    string Status,
    IReadOnlyList<string> Amenities);

public sealed record MetroJourneyLeg(
    byte LineId,
    string RouteId,
    string FromStationId,
    string FromStationName,
    string ToStationId,
    string ToStationName,
    string DirectionName,
    bool IsExpress,
    bool RequiresTransfer,
    int TransferWalkSeconds,
    DateTime? Departure,
    DateTime? Arrival,
    TimeSpan WaitTime,
    TimeSpan EstimatedRideTime,
    bool ScheduleFound);

public sealed record MetroNavigationResult(
    MetroNearestStation OriginStation,
    MetroNearestStation DestinationStation,
    double OriginWalkMeters,
    TimeSpan OriginWalkTime,
    double DestinationWalkMeters,
    TimeSpan DestinationWalkTime,
    IReadOnlyList<MetroJourneyLeg> Legs,
    DateTime RequestedAtTehran,
    DateTime EstimatedArrival,
    TimeSpan TotalTime);
