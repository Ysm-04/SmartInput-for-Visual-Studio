namespace SmartInput.VisualStudio
{
    internal sealed class SogouInputMethod : TsfInputMethodAdapter
    {
        protected override bool MatchesProfile(InputProcessorProfile profile)
        {
            return SupportedInputMethodProfile.IsSogouPinyin(
                profile.ProfileType, profile.LanguageId, profile.ClassId, profile.ProfileId);
        }

        public override string DisplayName { get { return "搜狗拼音"; } }
    }
}
