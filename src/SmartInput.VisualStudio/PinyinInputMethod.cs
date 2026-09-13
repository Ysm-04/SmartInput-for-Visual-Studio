namespace SmartInput.VisualStudio
{
    internal sealed class PinyinInputMethod : TsfInputMethodAdapter
    {
        protected override bool MatchesProfile(InputProcessorProfile profile)
        {
            return SupportedInputMethodProfile.IsMicrosoftPinyin(
                profile.ProfileType, profile.LanguageId, profile.ClassId, profile.ProfileId);
        }

        public override string DisplayName { get { return "微软拼音"; } }

        // Kept for the existing read-only Inspect-Pinyin.ps1 probe.
        public bool IsMicrosoftPinyin { get { return IsActive; } }
    }
}
