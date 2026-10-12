namespace FrameAnimator.App.Localization
{
    using System;
    using System.Windows.Data;
    using System.Windows.Markup;
    using CommunityToolkit.Mvvm.DependencyInjection;

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
            // no services at design time: the text is displayed as is
            ILocalizer? localizer = Ioc.Default.GetService<ILocalizer>();
            Binding binding = new(nameof(LocalizedText.Value))
            {
                Source = localizer?.Observe(Text) ?? new LocalizedText(Text),
                Mode = BindingMode.OneWay,
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}