using System;

namespace SmartInput.VisualStudio
{
    public static class SupportedInputMethodProfile
    {
        public const uint InputProcessorProfileType = 1;
        public const ushort SimplifiedChineseLanguageId = 0x0804;

        private static readonly Guid MicrosoftPinyinClassId = new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E");
        private static readonly Guid MicrosoftPinyinProfileId = new Guid("FA550B04-5AD7-411F-A5AC-CA038EC515D7");
        private static readonly Guid SogouClassId = new Guid("E7EA138E-69F8-11D7-A6EA-00065B844310");
        private static readonly Guid SogouProfileId = new Guid("E7EA138F-69F8-11D7-A6EA-00065B844311");

        public static bool IsMicrosoftPinyin(uint profileType, ushort languageId, Guid classId, Guid profileId)
        {
            return profileType == InputProcessorProfileType
                && languageId == SimplifiedChineseLanguageId
                && classId == MicrosoftPinyinClassId
                && profileId == MicrosoftPinyinProfileId;
        }

        public static bool IsSogouPinyin(uint profileType, ushort languageId, Guid classId, Guid profileId)
        {
            return profileType == InputProcessorProfileType
                && languageId == SimplifiedChineseLanguageId
                && classId == SogouClassId
                && profileId == SogouProfileId;
        }
    }
}
