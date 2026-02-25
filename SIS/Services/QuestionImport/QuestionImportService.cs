using SIS.Models.Import;
using SIS.Models.Assessments;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SIS.Services.QuestionImport
{
    public class QuestionImportService
    {
        private const int MAX_FILE_SIZE = 2 * 1024 * 1024; // 2MB
        private readonly string[] VALID_EXTENSIONS = { ".txt" };
        private readonly string[] VALID_QUESTION_TYPES = { "MultipleChoice", "TrueFalse", "ShortAnswer", "LongText" };

        /// <summary>
        /// Validates the uploaded file
        /// </summary>
        public (bool isValid, string errorMessage) ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "No file uploaded");
            }

            if (file.Length > MAX_FILE_SIZE)
            {
                return (false, $"File size exceeds maximum allowed size of {MAX_FILE_SIZE / (1024 * 1024)}MB");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!VALID_EXTENSIONS.Contains(extension))
            {
                return (false, "Only .txt files are supported");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Parses the text file and returns a list of imported questions
        /// </summary>
        public async Task<List<QuestionImportModel>> ParseTextFileAsync(Stream fileStream)
        {
            var questions = new List<QuestionImportModel>();
            var currentQuestion = new QuestionImportModel();
            var lineNumber = 0;
            var questionStartLine = 0;
            var temporaryId = 1;

            using (var reader = new StreamReader(fileStream))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    line = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // Question separator
                    if (line == "---")
                    {
                        if (!string.IsNullOrEmpty(currentQuestion.QuestionText))
                        {
                            currentQuestion.TemporaryId = temporaryId++;
                            currentQuestion.LineNumber = questionStartLine;
                            ValidateQuestion(currentQuestion);
                            questions.Add(currentQuestion);
                        }
                        currentQuestion = new QuestionImportModel();
                        questionStartLine = lineNumber + 1;
                        continue;
                    }

                    // Parse TYPE
                    if (line.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (questionStartLine == 0)
                            questionStartLine = lineNumber;

                        var typeText = line.Substring(5).Trim();
                        // Normalize type text (case-insensitive matching)
                        var matchedType = VALID_QUESTION_TYPES.FirstOrDefault(
                            t => t.Equals(typeText, StringComparison.OrdinalIgnoreCase)
                        );

                        if (matchedType != null)
                        {
                            currentQuestion.QuestionType = matchedType;
                        }
                        else
                        {
                            currentQuestion.QuestionType = typeText; // Keep it for validation error
                            currentQuestion.ValidationErrors.Add($"Invalid question type: '{typeText}'. Valid types are: MultipleChoice, TrueFalse, ShortAnswer, LongText");
                        }
                        continue;
                    }

                    // Parse QUESTION
                    if (line.StartsWith("QUESTION:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (questionStartLine == 0)
                            questionStartLine = lineNumber;

                        currentQuestion.QuestionText = line.Substring(9).Trim();
                        continue;
                    }

                    // Parse POINTS
                    if (line.StartsWith("POINTS:", StringComparison.OrdinalIgnoreCase))
                    {
                        var pointsText = line.Substring(7).Trim();
                        if (decimal.TryParse(pointsText, out decimal points))
                        {
                            currentQuestion.Points = points;
                        }
                        else
                        {
                            currentQuestion.ValidationErrors.Add($"Invalid points value: '{pointsText}'");
                        }
                        continue;
                    }

                    // Parse INFO
                    if (line.StartsWith("INFO:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentQuestion.AdditionalInfo = line.Substring(5).Trim();
                        continue;
                    }

                    // Parse ANSWER (for True/False)
                    if (line.StartsWith("ANSWER:", StringComparison.OrdinalIgnoreCase))
                    {
                        var answerText = line.Substring(7).Trim();
                        if (answerText.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            currentQuestion.TrueFalseAnswer = true;
                        }
                        else if (answerText.Equals("False", StringComparison.OrdinalIgnoreCase))
                        {
                            currentQuestion.TrueFalseAnswer = false;
                        }
                        else
                        {
                            currentQuestion.ValidationErrors.Add($"Invalid ANSWER value: '{answerText}'. Must be 'True' or 'False'");
                        }
                        continue;
                    }

                    // Parse EXPECTED_ANSWER (for Short Answer / Long Text)
                    if (line.StartsWith("EXPECTED_ANSWER:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentQuestion.ExpectedAnswer = line.Substring(16).Trim();
                        continue;
                    }

                    // Parse MAX_LENGTH
                    if (line.StartsWith("MAX_LENGTH:", StringComparison.OrdinalIgnoreCase))
                    {
                        var lengthText = line.Substring(11).Trim();
                        if (int.TryParse(lengthText, out int maxLength))
                        {
                            currentQuestion.MaxLength = maxLength;
                        }
                        else
                        {
                            currentQuestion.ValidationErrors.Add($"Invalid MAX_LENGTH value: '{lengthText}'");
                        }
                        continue;
                    }

                    // Parse MIN_LENGTH
                    if (line.StartsWith("MIN_LENGTH:", StringComparison.OrdinalIgnoreCase))
                    {
                        var lengthText = line.Substring(11).Trim();
                        if (int.TryParse(lengthText, out int minLength))
                        {
                            currentQuestion.MinLength = minLength;
                        }
                        else
                        {
                            currentQuestion.ValidationErrors.Add($"Invalid MIN_LENGTH value: '{lengthText}'");
                        }
                        continue;
                    }

                    // Parse EXPECTED_KEYWORDS
                    if (line.StartsWith("EXPECTED_KEYWORDS:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentQuestion.ExpectedKeywords = line.Substring(18).Trim();
                        continue;
                    }

                    // Parse OPTIONS (A), B), C), etc.) - for Multiple Choice
                    var optionMatch = Regex.Match(line, @"^([A-Z])\)\s*(.+)$", RegexOptions.IgnoreCase);
                    if (optionMatch.Success)
                    {
                        var letter = optionMatch.Groups[1].Value.ToUpper();
                        var text = optionMatch.Groups[2].Value.Trim();
                        var isCorrect = false;

                        // Check for [CORRECT] marker
                        if (text.EndsWith("[CORRECT]", StringComparison.OrdinalIgnoreCase))
                        {
                            isCorrect = true;
                            text = text.Substring(0, text.Length - 9).Trim();
                        }

                        currentQuestion.Options.Add(new ImportOptionModel(letter, text, isCorrect));
                    }
                }

                // Don't forget the last question if file doesn't end with ---
                if (!string.IsNullOrEmpty(currentQuestion.QuestionText))
                {
                    currentQuestion.TemporaryId = temporaryId++;
                    currentQuestion.LineNumber = questionStartLine > 0 ? questionStartLine : lineNumber;
                    ValidateQuestion(currentQuestion);
                    questions.Add(currentQuestion);
                }
            }

            return questions;
        }

        /// <summary>
        /// Validates a single question based on its type
        /// </summary>
        public void ValidateQuestion(QuestionImportModel question)
        {
            question.ValidationErrors.Clear();
            question.IsValid = true;

            // Common validation for all types
            if (string.IsNullOrWhiteSpace(question.QuestionText))
            {
                question.ValidationErrors.Add("Question text is required");
                question.IsValid = false;
            }

            if (question.Points <= 0)
            {
                question.ValidationErrors.Add("Points must be greater than 0");
                question.IsValid = false;
            }

            // Check if question type is valid
            if (!VALID_QUESTION_TYPES.Contains(question.QuestionType))
            {
                question.ValidationErrors.Add($"Invalid question type: '{question.QuestionType}'");
                question.IsValid = false;
                return; // Don't continue with type-specific validation
            }

            // Type-specific validation
            switch (question.QuestionType)
            {
                case "MultipleChoice":
                    ValidateMultipleChoice(question);
                    break;

                case "TrueFalse":
                    ValidateTrueFalse(question);
                    break;

                case "ShortAnswer":
                    ValidateShortAnswer(question);
                    break;

                case "LongText":
                    ValidateLongText(question);
                    break;
            }
        }

        private void ValidateMultipleChoice(QuestionImportModel question)
        {
            // Validate options
            if (question.Options.Count < 2)
            {
                question.ValidationErrors.Add("At least 2 options are required for Multiple Choice questions");
                question.IsValid = false;
            }

            // Validate correct answers
            var correctCount = question.Options.Count(o => o.IsCorrect);
            if (correctCount == 0)
            {
                question.ValidationErrors.Add("At least one option must be marked as correct");
                question.IsValid = false;
            }

            // Validate option letters are sequential
            if (question.Options.Count > 0)
            {
                var expectedLetters = GenerateOptionLetters(question.Options.Count);
                var actualLetters = question.Options.Select(o => o.OptionLetter).ToList();

                if (!expectedLetters.SequenceEqual(actualLetters))
                {
                    question.ValidationErrors.Add("Option letters must be sequential (A, B, C, D, ...)");
                    question.IsValid = false;
                }
            }

            // Validate empty option text
            if (question.Options.Any(o => string.IsNullOrWhiteSpace(o.OptionText)))
            {
                question.ValidationErrors.Add("All options must have text");
                question.IsValid = false;
            }
        }

        private void ValidateTrueFalse(QuestionImportModel question)
        {
            if (!question.TrueFalseAnswer.HasValue)
            {
                question.ValidationErrors.Add("ANSWER field is required for True/False questions (must be 'True' or 'False')");
                question.IsValid = false;
            }

            // True/False shouldn't have options in the template (they're auto-generated)
            if (question.Options.Any())
            {
                question.ValidationErrors.Add("True/False questions should not have options listed. Use ANSWER: True or ANSWER: False instead");
                question.IsValid = false;
            }
        }

        private void ValidateShortAnswer(QuestionImportModel question)
        {
            // MaxLength validation
            if (question.MaxLength.HasValue && question.MaxLength.Value <= 0)
            {
                question.ValidationErrors.Add("MAX_LENGTH must be greater than 0");
                question.IsValid = false;
            }

            // Short answer shouldn't have options
            if (question.Options.Any())
            {
                question.ValidationErrors.Add("Short Answer questions should not have options");
                question.IsValid = false;
            }
        }

        private void ValidateLongText(QuestionImportModel question)
        {
            // MinLength validation
            if (question.MinLength.HasValue && question.MinLength.Value <= 0)
            {
                question.ValidationErrors.Add("MIN_LENGTH must be greater than 0");
                question.IsValid = false;
            }

            // MaxLength validation
            if (question.MaxLength.HasValue && question.MaxLength.Value <= 0)
            {
                question.ValidationErrors.Add("MAX_LENGTH must be greater than 0");
                question.IsValid = false;
            }

            // Cross-validation
            if (question.MinLength.HasValue && question.MaxLength.HasValue &&
                question.MinLength.Value >= question.MaxLength.Value)
            {
                question.ValidationErrors.Add("MIN_LENGTH must be less than MAX_LENGTH");
                question.IsValid = false;
            }

            // Long text shouldn't have options
            if (question.Options.Any())
            {
                question.ValidationErrors.Add("Long Text questions should not have options");
                question.IsValid = false;
            }
        }

        /// <summary>
        /// Converts an import model to a Question entity
        /// </summary>
        public Question ConvertToQuestion(QuestionImportModel importModel, int questionGroupId, string createdBy)
        {
            var question = new Question
            {
                QuestionGroupId = questionGroupId,
                QuestionText = importModel.QuestionText,
                QuestionType = importModel.QuestionType,
                Points = importModel.Points,
                AdditionalInfo = importModel.AdditionalInfo,
                IsActive = true,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now,
                Options = new List<QuestionOption>()
            };

            // Type-specific conversion
            switch (importModel.QuestionType)
            {
                case "MultipleChoice":
                    ConvertMultipleChoiceOptions(importModel, question, createdBy);
                    break;

                case "TrueFalse":
                    ConvertTrueFalseOptions(importModel, question, createdBy);
                    break;

                case "ShortAnswer":
                    ConvertShortAnswerMetadata(importModel, question);
                    break;

                case "LongText":
                    ConvertLongTextMetadata(importModel, question);
                    break;
            }

            return question;
        }

        private void ConvertMultipleChoiceOptions(QuestionImportModel importModel, Question question, string createdBy)
        {
            foreach (var option in importModel.Options)
            {
                question.Options.Add(new QuestionOption
                {
                    OptionText = option.OptionText,
                    IsCorrect = option.IsCorrect,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.Now
                });
            }
        }

        private void ConvertTrueFalseOptions(QuestionImportModel importModel, Question question, string createdBy)
        {
            // Create True option
            question.Options.Add(new QuestionOption
            {
                OptionText = "True",
                IsCorrect = importModel.TrueFalseAnswer.HasValue && importModel.TrueFalseAnswer.Value,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });

            // Create False option
            question.Options.Add(new QuestionOption
            {
                OptionText = "False",
                IsCorrect = importModel.TrueFalseAnswer.HasValue && !importModel.TrueFalseAnswer.Value,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            });
        }

        private void ConvertShortAnswerMetadata(QuestionImportModel importModel, Question question)
        {
            // Store metadata as JSON in AdditionalInfo (appended to existing info if any)
            var metadata = new
            {
                ExpectedAnswer = importModel.ExpectedAnswer,
                MaxLength = importModel.MaxLength
            };

            var metadataJson = JsonSerializer.Serialize(metadata);

            if (!string.IsNullOrWhiteSpace(question.AdditionalInfo))
            {
                question.AdditionalInfo += "\n\n[METADATA]" + metadataJson;
            }
            else
            {
                question.AdditionalInfo = "[METADATA]" + metadataJson;
            }
        }

        private void ConvertLongTextMetadata(QuestionImportModel importModel, Question question)
        {
            // Store metadata as JSON in AdditionalInfo (appended to existing info if any)
            var metadata = new
            {
                ExpectedAnswer = importModel.ExpectedAnswer,
                MinLength = importModel.MinLength,
                MaxLength = importModel.MaxLength,
                ExpectedKeywords = importModel.ExpectedKeywords
            };

            var metadataJson = JsonSerializer.Serialize(metadata);

            if (!string.IsNullOrWhiteSpace(question.AdditionalInfo))
            {
                question.AdditionalInfo += "\n\n[METADATA]" + metadataJson;
            }
            else
            {
                question.AdditionalInfo = "[METADATA]" + metadataJson;
            }
        }

        /// <summary>
        /// Generates expected option letters
        /// </summary>
        private List<string> GenerateOptionLetters(int count)
        {
            var letters = new List<string>();
            for (int i = 0; i < count; i++)
            {
                letters.Add(((char)('A' + i)).ToString());
            }
            return letters;
        }

        /// <summary>
        /// Generates a comprehensive sample template file content for all question types
        /// </summary>
        public string GenerateTemplateContent()
        {
            return @"# QUESTION IMPORT TEMPLATE - ALL QUESTION TYPES
# ================================================
# 
# INSTRUCTIONS:
# 1. Each question must be separated by three dashes (---)
# 2. TYPE: Specify the question type (REQUIRED for TrueFalse, ShortAnswer, LongText)
#    Valid types: MultipleChoice, TrueFalse, ShortAnswer, LongText
# 3. QUESTION: Your question text (REQUIRED)
# 4. POINTS: Point value for the question (REQUIRED, must be > 0)
# 5. Type-specific fields (see examples below)
# 6. INFO: Additional instructions or context (OPTIONAL)
# 7. Lines starting with # are comments and will be ignored
#
# ================================================================
# MULTIPLE CHOICE QUESTIONS
# ================================================================
# - Options: A), B), C), D), etc. (MINIMUM 2 REQUIRED)
# - Mark correct answers by adding [CORRECT] after the option text
# - Multiple correct answers allowed
#

TYPE: MultipleChoice
QUESTION: What is the capital of France?
POINTS: 1
A) London
B) Paris [CORRECT]
C) Berlin
D) Madrid
INFO: Basic geography question
---

TYPE: MultipleChoice
QUESTION: Which of these are primary colors in the RGB model?
POINTS: 2
A) Red [CORRECT]
B) Green [CORRECT]
C) Blue [CORRECT]
D) Yellow
INFO: Multiple correct answers
---

# ================================================================
# TRUE/FALSE QUESTIONS
# ================================================================
# - ANSWER: True or False (REQUIRED)
# - Do NOT list A) and B) options - they are auto-generated
#

TYPE: TrueFalse
QUESTION: The Earth revolves around the Sun.
POINTS: 1
ANSWER: True
INFO: Basic astronomy fact
---

TYPE: TrueFalse
QUESTION: Python is a compiled programming language.
POINTS: 1
ANSWER: False
INFO: Python is an interpreted language
---

# ================================================================
# SHORT ANSWER QUESTIONS
# ================================================================
# - EXPECTED_ANSWER: Sample/expected answer (OPTIONAL, for instructor reference)
# - MAX_LENGTH: Maximum character limit (OPTIONAL)
# - These questions require manual grading
#

TYPE: ShortAnswer
QUESTION: What is the chemical symbol for water?
POINTS: 2
EXPECTED_ANSWER: H2O
MAX_LENGTH: 10
INFO: One or two word answer expected
---

TYPE: ShortAnswer
QUESTION: Name the inventor of the telephone.
POINTS: 1
EXPECTED_ANSWER: Alexander Graham Bell
MAX_LENGTH: 50
---

# ================================================================
# LONG TEXT QUESTIONS
# ================================================================
# - EXPECTED_ANSWER: Sample/expected answer (OPTIONAL, for instructor reference)
# - MIN_LENGTH: Minimum character requirement (OPTIONAL)
# - MAX_LENGTH: Maximum character limit (OPTIONAL)
# - EXPECTED_KEYWORDS: Keywords for grading guidance (OPTIONAL, comma-separated)
# - These questions require manual grading
#

TYPE: LongText
QUESTION: Explain the concept of Object-Oriented Programming and list its main principles.
POINTS: 5
MIN_LENGTH: 100
MAX_LENGTH: 500
EXPECTED_KEYWORDS: encapsulation, inheritance, polymorphism, abstraction
INFO: Detailed explanation required with examples
---

TYPE: LongText
QUESTION: Describe the water cycle and its importance to the environment.
POINTS: 10
MIN_LENGTH: 200
MAX_LENGTH: 1000
EXPECTED_ANSWER: The water cycle is a continuous process where water evaporates from bodies of water, forms clouds, precipitates as rain or snow, and returns to bodies of water. It is essential for distributing water resources, regulating climate, and supporting all forms of life.
EXPECTED_KEYWORDS: evaporation, condensation, precipitation, collection, climate
INFO: Include all stages of the cycle
---

# ================================================================
# TIPS:
# ================================================================
# - Keep question text clear and concise
# - Ensure all required fields are present for each question type
# - TYPE field defaults to MultipleChoice if omitted (for backward compatibility)
# - Points can be decimals (e.g., 1.5, 2.25)
# - After import, you can edit questions to add rich formatting and images
# - Expected answers and keywords are for instructor reference only
#
# Add your questions below this line:
# ====================================

";
        }
    }
}