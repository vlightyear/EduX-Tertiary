namespace EduX.Models.GeoPolitical
{
    public class Nation
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        // Navigation
        public ICollection<Province> Provinces { get; set; } = new List<Province>();
    }

    public class Province
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int NationId { get; set; }

        // Navigation
        public Nation? Nation { get; set; }
        public ICollection<District> Districts { get; set; } = new List<District>();
    }

    public class District
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int ProvinceId { get; set; }

        // Navigation
        public Province? Province { get; set; }
        public ICollection<Constituency> Constituencies { get; set; } = new List<Constituency>();
    }

    public class Constituency
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int DistrictId { get; set; }

        // Navigation
        public District? District { get; set; }
        public ICollection<Ward> Wards { get; set; } = new List<Ward>();
    }

    public class Ward
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Foreign Key
        public int ConstituencyId { get; set; }

        // Navigation
        public Constituency? Constituency { get; set; }
    }
}
