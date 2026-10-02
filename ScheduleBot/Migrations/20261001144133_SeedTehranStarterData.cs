using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScheduleBot.Migrations
{
    /// <inheritdoc />
    public partial class SeedTehranStarterData : Migration
    {
        private static readonly DateTime SeededAtUtc =
            new(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "TeaMapPlaces",
                columns: new[]
                {
                    "Id", "Name", "PlaceType", "ExternalId", "Source", "Latitude", "Longitude",
                    "Priority", "IsActive", "CreatedAtUtc", "UpdatedAtUtc"
                },
                values: new object[,]
                {
                    { Guid.Parse("10000000-0000-0000-0000-000000000001"), "Mirdamad Boulevard", "street", "starter-mirdamad", "starter", 35.7595m, 51.4305m, 75, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000002"), "Vanak Square", "square", "starter-vanak", "starter", 35.7580m, 51.4095m, 75, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000003"), "Shariati Street", "street", "starter-shariati", "starter", 35.7640m, 51.4380m, 60, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000004"), "Modarres Expressway", "street", "starter-modarres", "starter", 35.7540m, 51.4250m, 80, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000005"), "Hemat Expressway", "street", "starter-hemmat", "starter", 35.7700m, 51.4200m, 65, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000006"), "Sadr Expressway", "street", "starter-sadr", "starter", 35.7860m, 51.4400m, 65, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000007"), "Tabiat Bridge", "landmark", "starter-tabiat", "starter", 35.7570m, 51.4165m, 80, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000008"), "Tajrish Square", "square", "starter-tajrish", "starter", 35.8055m, 51.4285m, 95, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000009"), "Darband", "landmark", "starter-darband", "starter", 35.8190m, 51.4255m, 60, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000010"), "Saadabad Complex", "landmark", "starter-saadabad", "starter", 35.8230m, 51.4250m, 38, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000011"), "Mellat Park", "park", "starter-mellat", "starter", 35.7790m, 51.4070m, 40, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000012"), "Shahid Beheshti Street", "street", "starter-beheshti", "starter", 35.7350m, 51.4350m, 73, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000013"), "Motahari Street", "street", "starter-motahari", "starter", 35.7300m, 51.4250m, 68, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000014"), "Haft-e Tir Square", "square", "starter-haft-e-tir", "starter", 35.7215m, 51.4250m, 68, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000015"), "Enghelab Street", "street", "starter-enghelab", "starter", 35.7040m, 51.4050m, 95, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000016"), "University of Tehran", "landmark", "starter-tehran-university", "starter", 35.7050m, 51.3980m, 60, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000017"), "Keshavarz Boulevard", "street", "starter-keshavarz", "starter", 35.7155m, 51.3985m, 68, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000018"), "Laleh Park", "park", "starter-laleh", "starter", 35.7175m, 51.3915m, 55, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000019"), "Azadi Square", "square", "starter-azadi", "starter", 35.6997m, 51.3370m, 45, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000020"), "Azadi Tower", "landmark", "starter-azadi-tower", "starter", 35.6997m, 51.3375m, 30, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000021"), "Milad Tower", "landmark", "starter-milad", "starter", 35.7445m, 51.3750m, 30, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000022"), "Imam Khomeini Square", "square", "starter-imam-khomeini", "starter", 35.6890m, 51.4150m, 75, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000023"), "Tehran Grand Bazaar", "landmark", "starter-grand-bazaar", "starter", 35.6765m, 51.4215m, 80, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000024"), "Golestan Palace", "landmark", "starter-golestan", "starter", 35.6795m, 51.4205m, 80, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000025"), "Tehran Railway Station", "landmark", "starter-railway", "starter", 35.6580m, 51.3915m, 53, true, SeededAtUtc, SeededAtUtc },
                    { Guid.Parse("10000000-0000-0000-0000-000000000026"), "Imam Hossein Square", "square", "starter-imam-hossein", "starter", 35.7020m, 51.4550m, 38, true, SeededAtUtc, SeededAtUtc }
                });

            migrationBuilder.InsertData(
                table: "TeaMapPlaceAliases",
                columns: new[] { "Id", "PlaceId", "Name", "LanguageCode", "AliasType" },
                values: new object[,]
                {
                    { Guid.Parse("30000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000001"), "بلوار میرداماد", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000002"), "میدان ونک", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000003"), Guid.Parse("10000000-0000-0000-0000-000000000003"), "خیابان شریعتی", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000004"), Guid.Parse("10000000-0000-0000-0000-000000000004"), "بزرگراه مدرس", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000005"), Guid.Parse("10000000-0000-0000-0000-000000000005"), "بزرگراه همت", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000006"), Guid.Parse("10000000-0000-0000-0000-000000000006"), "بزرگراه صدر", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000007"), Guid.Parse("10000000-0000-0000-0000-000000000007"), "پل طبیعت", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000008"), Guid.Parse("10000000-0000-0000-0000-000000000008"), "میدان تجریش", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000009"), Guid.Parse("10000000-0000-0000-0000-000000000009"), "دربند", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000010"), Guid.Parse("10000000-0000-0000-0000-000000000010"), "مجموعه سعدآباد", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000011"), Guid.Parse("10000000-0000-0000-0000-000000000011"), "پارک ملت", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000012"), Guid.Parse("10000000-0000-0000-0000-000000000012"), "خیابان شهید بهشتی", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000013"), Guid.Parse("10000000-0000-0000-0000-000000000013"), "خیابان مطهری", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000014"), Guid.Parse("10000000-0000-0000-0000-000000000014"), "میدان هفت تیر", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000015"), Guid.Parse("10000000-0000-0000-0000-000000000015"), "خیابان انقلاب اسلامی", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000016"), Guid.Parse("10000000-0000-0000-0000-000000000016"), "دانشگاه تهران", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000017"), Guid.Parse("10000000-0000-0000-0000-000000000017"), "بلوار کشاورز", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000018"), Guid.Parse("10000000-0000-0000-0000-000000000018"), "پارک لاله", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000019"), Guid.Parse("10000000-0000-0000-0000-000000000019"), "میدان آزادی", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000020"), Guid.Parse("10000000-0000-0000-0000-000000000020"), "برج آزادی", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000021"), Guid.Parse("10000000-0000-0000-0000-000000000021"), "برج میلاد", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000022"), Guid.Parse("10000000-0000-0000-0000-000000000022"), "میدان امام خمینی", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000023"), Guid.Parse("10000000-0000-0000-0000-000000000023"), "بازار بزرگ تهران", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000024"), Guid.Parse("10000000-0000-0000-0000-000000000024"), "کاخ گلستان", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000025"), Guid.Parse("10000000-0000-0000-0000-000000000025"), "ایستگاه راه آهن تهران", "fa", "name" },
                    { Guid.Parse("30000000-0000-0000-0000-000000000026"), Guid.Parse("10000000-0000-0000-0000-000000000026"), "میدان امام حسین", "fa", "name" }
                });

            migrationBuilder.InsertData(
                table: "TeaMapLessons",
                columns: new[] { "Id", "Name", "Description", "Position", "Difficulty", "IsPublished", "CreatedAtUtc", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { Guid.Parse("20000000-0000-0000-0000-000000000001"), "Tehran Starter: North and Central", "A first lesson covering high-priority Tehran landmarks, squares, and connecting roads.", 1, 1, true, SeededAtUtc, SeededAtUtc }
                });

            migrationBuilder.InsertData(
                table: "TeaMapLessonPlaces",
                columns: new[] { "Id", "LessonId", "PlaceId", "Position" },
                values: new object[,]
                {
                    { Guid.Parse("40000000-0000-0000-0000-000000000001"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000008"), 0 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000002"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000015"), 1 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000003"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000004"), 2 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000004"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000007"), 3 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000005"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000001"), 4 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000006"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"), 5 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000007"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000012"), 6 },
                    { Guid.Parse("40000000-0000-0000-0000-000000000008"), Guid.Parse("20000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000023"), 7 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("TeaMapLessonPlaces", "Id", new object[]
            {
                Guid.Parse("40000000-0000-0000-0000-000000000001"), Guid.Parse("40000000-0000-0000-0000-000000000002"),
                Guid.Parse("40000000-0000-0000-0000-000000000003"), Guid.Parse("40000000-0000-0000-0000-000000000004"),
                Guid.Parse("40000000-0000-0000-0000-000000000005"), Guid.Parse("40000000-0000-0000-0000-000000000006"),
                Guid.Parse("40000000-0000-0000-0000-000000000007"), Guid.Parse("40000000-0000-0000-0000-000000000008")
            });

            migrationBuilder.DeleteData("TeaMapLessons", "Id", Guid.Parse("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData("TeaMapPlaceAliases", "Id", new object[]
            {
                Guid.Parse("30000000-0000-0000-0000-000000000001"), Guid.Parse("30000000-0000-0000-0000-000000000002"),
                Guid.Parse("30000000-0000-0000-0000-000000000003"), Guid.Parse("30000000-0000-0000-0000-000000000004"),
                Guid.Parse("30000000-0000-0000-0000-000000000005"), Guid.Parse("30000000-0000-0000-0000-000000000006"),
                Guid.Parse("30000000-0000-0000-0000-000000000007"), Guid.Parse("30000000-0000-0000-0000-000000000008"),
                Guid.Parse("30000000-0000-0000-0000-000000000009"), Guid.Parse("30000000-0000-0000-0000-000000000010"),
                Guid.Parse("30000000-0000-0000-0000-000000000011"), Guid.Parse("30000000-0000-0000-0000-000000000012"),
                Guid.Parse("30000000-0000-0000-0000-000000000013"), Guid.Parse("30000000-0000-0000-0000-000000000014"),
                Guid.Parse("30000000-0000-0000-0000-000000000015"), Guid.Parse("30000000-0000-0000-0000-000000000016"),
                Guid.Parse("30000000-0000-0000-0000-000000000017"), Guid.Parse("30000000-0000-0000-0000-000000000018"),
                Guid.Parse("30000000-0000-0000-0000-000000000019"), Guid.Parse("30000000-0000-0000-0000-000000000020"),
                Guid.Parse("30000000-0000-0000-0000-000000000021"), Guid.Parse("30000000-0000-0000-0000-000000000022"),
                Guid.Parse("30000000-0000-0000-0000-000000000023"), Guid.Parse("30000000-0000-0000-0000-000000000024"),
                Guid.Parse("30000000-0000-0000-0000-000000000025"), Guid.Parse("30000000-0000-0000-0000-000000000026")
            });

            migrationBuilder.DeleteData("TeaMapPlaces", "Id", new object[]
            {
                Guid.Parse("10000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"),
                Guid.Parse("10000000-0000-0000-0000-000000000003"), Guid.Parse("10000000-0000-0000-0000-000000000004"),
                Guid.Parse("10000000-0000-0000-0000-000000000005"), Guid.Parse("10000000-0000-0000-0000-000000000006"),
                Guid.Parse("10000000-0000-0000-0000-000000000007"), Guid.Parse("10000000-0000-0000-0000-000000000008"),
                Guid.Parse("10000000-0000-0000-0000-000000000009"), Guid.Parse("10000000-0000-0000-0000-000000000010"),
                Guid.Parse("10000000-0000-0000-0000-000000000011"), Guid.Parse("10000000-0000-0000-0000-000000000012"),
                Guid.Parse("10000000-0000-0000-0000-000000000013"), Guid.Parse("10000000-0000-0000-0000-000000000014"),
                Guid.Parse("10000000-0000-0000-0000-000000000015"), Guid.Parse("10000000-0000-0000-0000-000000000016"),
                Guid.Parse("10000000-0000-0000-0000-000000000017"), Guid.Parse("10000000-0000-0000-0000-000000000018"),
                Guid.Parse("10000000-0000-0000-0000-000000000019"), Guid.Parse("10000000-0000-0000-0000-000000000020"),
                Guid.Parse("10000000-0000-0000-0000-000000000021"), Guid.Parse("10000000-0000-0000-0000-000000000022"),
                Guid.Parse("10000000-0000-0000-0000-000000000023"), Guid.Parse("10000000-0000-0000-0000-000000000024"),
                Guid.Parse("10000000-0000-0000-0000-000000000025"), Guid.Parse("10000000-0000-0000-0000-000000000026")
            });
        }
    }
}
