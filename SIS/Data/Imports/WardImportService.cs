using CsvHelper;
using EduX.Models.GeoPolitical;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using System.Globalization;

namespace EduX.Data.Imports
{
    public class WardImportService
    {
        private readonly ApplicationDbContext _context;

        public WardImportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ImportAsync(IEnumerable<Ward> wards)
        {
            int wardCount = await _context.Wards.CountAsync();

            if(!_context.Wards.Any())
            {
                await _context.Wards.AddRangeAsync(wards);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Ward>> LoadWardsFromCsv(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            return csv.GetRecords<Ward>().ToList();
        }
    }
}
