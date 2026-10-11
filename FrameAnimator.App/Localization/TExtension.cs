namespace FrameAnimator.App.Localization
{
    using System;
    using System.Windows.Data;
    using System.Windows.Markup;

    /// <summary>XAML markup extension that displays a translated text: <c>{loc:T 'Some text'}</c>.</summary>
    [MarkupExtensionReturnType(typeof(String))]
    public sealed class TExtension : MarkupExtension
    {
        public TExtension(String text)
        {
            Text = text;
        }

        public String Text { get; set; }

        public override Object ProvideValue(IServiceProvider serviceProvider)
        {
            Binding binding = new(nameof(LocalizedText.Value))
            {
                Source = Localizer.Instance.Observe(Text),
                Mode = BindingMode.OneWay,
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
