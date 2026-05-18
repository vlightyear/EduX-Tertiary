using EduX.Models.GeoPolitical;
using Microsoft.EntityFrameworkCore;

namespace EduX.Data.Seeders;

public static class ZambiaSeedData
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        SeedNations(modelBuilder);
        SeedProvinces(modelBuilder);
        SeedDistricts(modelBuilder);
        SeedConstituencies(modelBuilder);
    }

    private static void SeedNations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Nation>().HasData(
            new Nation
            {
                Id = 1,
                Name = "Zambia",
                Code = "ZM"
            }
        );
    }

    private static void SeedProvinces(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Province>().HasData(
            new Province { Id = 1, Name = "Central", NationId = 1 },
            new Province { Id = 2, Name = "Copperbelt", NationId = 1 },
            new Province { Id = 3, Name = "Eastern", NationId = 1 },
            new Province { Id = 4, Name = "Luapula", NationId = 1 },
            new Province { Id = 5, Name = "Lusaka", NationId = 1 },
            new Province { Id = 6, Name = "Muchinga", NationId = 1 },
            new Province { Id = 7, Name = "Northern", NationId = 1 },
            new Province { Id = 8, Name = "North-Western", NationId = 1 },
            new Province { Id = 9, Name = "Southern", NationId = 1 },
            new Province { Id = 10, Name = "Western", NationId = 1 }
        );
    }

    private static void SeedDistricts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<District>().HasData(

            // Central
            new District { Id = 1, Name = "Chibombo", ProvinceId = 1 },
            new District { Id = 2, Name = "Chisamba", ProvinceId = 1 },
            new District { Id = 3, Name = "Chitambo", ProvinceId = 1 },
            new District { Id = 4, Name = "Kabwe", ProvinceId = 1 },
            new District { Id = 5, Name = "Kapiri Mposhi", ProvinceId = 1 },
            new District { Id = 6, Name = "Luano", ProvinceId = 1 },
            new District { Id = 7, Name = "Mkushi", ProvinceId = 1 },
            new District { Id = 8, Name = "Mumbwa", ProvinceId = 1 },
            new District { Id = 9, Name = "Ngabwe", ProvinceId = 1 },
            new District { Id = 10, Name = "Serenje", ProvinceId = 1 },
            new District { Id = 11, Name = "Shibuyunji", ProvinceId = 1 },

            // Copperbelt
            new District { Id = 12, Name = "Chililabombwe", ProvinceId = 2 },
            new District { Id = 13, Name = "Chingola", ProvinceId = 2 },
            new District { Id = 14, Name = "Kalulushi", ProvinceId = 2 },
            new District { Id = 15, Name = "Kitwe", ProvinceId = 2 },
            new District { Id = 16, Name = "Luanshya", ProvinceId = 2 },
            new District { Id = 17, Name = "Lufwanyama", ProvinceId = 2 },
            new District { Id = 18, Name = "Masaiti", ProvinceId = 2 },
            new District { Id = 19, Name = "Mpongwe", ProvinceId = 2 },
            new District { Id = 20, Name = "Mufulira", ProvinceId = 2 },
            new District { Id = 21, Name = "Ndola", ProvinceId = 2 },

            // Eastern
            new District { Id = 22, Name = "Chadiza", ProvinceId = 3 },
            new District { Id = 23, Name = "Chasefu", ProvinceId = 3 },
            new District { Id = 24, Name = "Chipangali", ProvinceId = 3 },
            new District { Id = 25, Name = "Chipata", ProvinceId = 3 },
            new District { Id = 26, Name = "Katete", ProvinceId = 3 },
            new District { Id = 27, Name = "Kasenengwa", ProvinceId = 3 },
            new District { Id = 28, Name = "Lundazi", ProvinceId = 3 },
            new District { Id = 29, Name = "Lusangazi", ProvinceId = 3 },
            new District { Id = 30, Name = "Lumezi", ProvinceId = 3 },
            new District { Id = 31, Name = "Mambwe", ProvinceId = 3 },
            new District { Id = 32, Name = "Nyimba", ProvinceId = 3 },
            new District { Id = 33, Name = "Petauke", ProvinceId = 3 },
            new District { Id = 34, Name = "Sinda", ProvinceId = 3 },
            new District { Id = 35, Name = "Vubwi", ProvinceId = 3 },
            new District { Id = 36, Name = "Mkaika", ProvinceId = 3 },

            // Luapula
            new District { Id = 37, Name = "Chembe", ProvinceId = 4 },
            new District { Id = 38, Name = "Chiengi", ProvinceId = 4 },
            new District { Id = 39, Name = "Chifunabuli", ProvinceId = 4 },
            new District { Id = 40, Name = "Chipili", ProvinceId = 4 },
            new District { Id = 41, Name = "Kawambwa", ProvinceId = 4 },
            new District { Id = 42, Name = "Lunga", ProvinceId = 4 },
            new District { Id = 43, Name = "Mansa", ProvinceId = 4 },
            new District { Id = 44, Name = "Milenge", ProvinceId = 4 },
            new District { Id = 45, Name = "Mwansabombwe", ProvinceId = 4 },
            new District { Id = 46, Name = "Mwense", ProvinceId = 4 },
            new District { Id = 47, Name = "Nchelenge", ProvinceId = 4 },
            new District { Id = 48, Name = "Samfya", ProvinceId = 4 },

            // Lusaka
            new District { Id = 49, Name = "Chilanga", ProvinceId = 5 },
            new District { Id = 50, Name = "Chongwe", ProvinceId = 5 },
            new District { Id = 51, Name = "Kafue", ProvinceId = 5 },
            new District { Id = 52, Name = "Luangwa", ProvinceId = 5 },
            new District { Id = 53, Name = "Lusaka", ProvinceId = 5 },
            new District { Id = 54, Name = "Rufunsa", ProvinceId = 5 }
        );
    }

    private static void SeedConstituencies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Constituency>().HasData(

            // Central Province
            new Constituency { Id = 1, Name = "Bwacha", DistrictId = 4 },
            new Constituency { Id = 2, Name = "Chisamba", DistrictId = 2 },
            new Constituency { Id = 3, Name = "Chitambo", DistrictId = 3 },
            new Constituency { Id = 4, Name = "Kabwe Central", DistrictId = 4 },
            new Constituency { Id = 5, Name = "Kapiri Mposhi", DistrictId = 5 },
            new Constituency { Id = 6, Name = "Katuba", DistrictId = 1 },
            new Constituency { Id = 7, Name = "Keembe", DistrictId = 1 },
            new Constituency { Id = 8, Name = "Mkushi North", DistrictId = 7 },
            new Constituency { Id = 9, Name = "Mkushi South", DistrictId = 7 },
            new Constituency { Id = 10, Name = "Mumbwa", DistrictId = 8 },
            new Constituency { Id = 11, Name = "Serenje", DistrictId = 10 },

            // Copperbelt
            new Constituency { Id = 12, Name = "Bwana Mkubwa", DistrictId = 21 },
            new Constituency { Id = 13, Name = "Chifubu", DistrictId = 21 },
            new Constituency { Id = 14, Name = "Chililabombwe", DistrictId = 12 },
            new Constituency { Id = 15, Name = "Chimwemwe", DistrictId = 15 },
            new Constituency { Id = 16, Name = "Chingola", DistrictId = 13 },
            new Constituency { Id = 17, Name = "Kabushi", DistrictId = 15 },
            new Constituency { Id = 18, Name = "Kalulushi", DistrictId = 14 },
            new Constituency { Id = 19, Name = "Kwacha", DistrictId = 15 },
            new Constituency { Id = 20, Name = "Luanshya", DistrictId = 16 },
            new Constituency { Id = 21, Name = "Mufulira", DistrictId = 20 },
            new Constituency { Id = 22, Name = "Ndola Central", DistrictId = 21 },
            new Constituency { Id = 23, Name = "Nkana", DistrictId = 15 },
            new Constituency { Id = 24, Name = "Roan", DistrictId = 16 },
            new Constituency { Id = 25, Name = "Wusakile", DistrictId = 15 },

            // Lusaka Province
            new Constituency { Id = 26, Name = "Chawama", DistrictId = 53 },
            new Constituency { Id = 27, Name = "Chilanga", DistrictId = 49 },
            new Constituency { Id = 28, Name = "Chongwe", DistrictId = 50 },
            new Constituency { Id = 29, Name = "Kabwata", DistrictId = 53 },
            new Constituency { Id = 30, Name = "Kafue", DistrictId = 51 },
            new Constituency { Id = 31, Name = "Kanyama", DistrictId = 53 },
            new Constituency { Id = 32, Name = "Lusaka Central", DistrictId = 53 },
            new Constituency { Id = 33, Name = "Mandevu", DistrictId = 53 },
            new Constituency { Id = 34, Name = "Matero", DistrictId = 53 },
            new Constituency { Id = 35, Name = "Munali", DistrictId = 53 },
            new Constituency { Id = 36, Name = "Rufunsa", DistrictId = 54 }
        );
    }
}