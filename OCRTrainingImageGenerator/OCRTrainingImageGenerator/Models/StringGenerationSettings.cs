using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OCRTrainingImageGenerator.Models
{
    [Serializable]
    public class StringGenerationSettings
    {
        public CharacterSetSettings CharacterSets { get; set; } = new CharacterSetSettings();
        public GenerationMode Mode { get; set; } = GenerationMode.RuleBased;
        public StartEndRules StartRules { get; set; } = new StartEndRules();
        public StartEndRules EndRules { get; set; } = new StartEndRules();
        public LengthSettings Length { get; set; } = new LengthSettings();
        public int Quantity { get; set; } = 1000;
        public bool UniqueOnly { get; set; } = true;
        public OcrChallengeSettings OcrChallenges { get; set; } = new OcrChallengeSettings();
        public string OutputPath { get; set; } = "strings.txt";
        public bool AppendMode { get; set; } = false;
    }

    [Serializable]
    public class CharacterSetSettings
    {
        public bool IncludeUppercase { get; set; } = true;
        public bool IncludeLowercase { get; set; } = true;
        public bool IncludeNumbers { get; set; } = true;
        public string SpecialCharacters { get; set; } = "_-.";
    }

    [Serializable]
    public class StartEndRules
    {
        public bool AllowUppercase { get; set; } = true;
        public bool AllowLowercase { get; set; } = true;
        public bool AllowNumbers { get; set; } = false;
        public bool AllowSpecialChars { get; set; } = false;
    }

    [Serializable]
    public class LengthSettings
    {
        public bool UseRange { get; set; } = true;
        public int FixedLength { get; set; } = 10;
        public int MinLength { get; set; } = 3;
        public int MaxLength { get; set; } = 15;
    }

    [Serializable]
    public class OcrChallengeSettings
    {
        public bool Enabled { get; set; } = false;
        public int ConfusionPercentage { get; set; } = 20;
    }

    public enum GenerationMode
    {
        RuleBased,
        PurelyRandom
    }
}