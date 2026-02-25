using System.Text.Json.Serialization;

namespace SIS.Models.Import
{
    public class ImportOptionModel
    {
        [JsonPropertyName("OptionLetter")]
        public string OptionLetter { get; set; }

        [JsonPropertyName("OptionText")]
        public string OptionText { get; set; }

        [JsonPropertyName("IsCorrect")]
        public bool IsCorrect { get; set; }

        // Parameterless constructor for JSON deserialization
        public ImportOptionModel()
        {
            OptionLetter = string.Empty;
            OptionText = string.Empty;
            IsCorrect = false;
        }

        // Parameterized constructor for code usage
        public ImportOptionModel(string letter, string text, bool isCorrect = false)
        {
            OptionLetter = letter;
            OptionText = text;
            IsCorrect = isCorrect;
        }
    }
}