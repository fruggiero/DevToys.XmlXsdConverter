using DevToys.Api;
using System.ComponentModel.Composition;
using System.Xml;
using System.Xml.Schema;
using DevToys.XmlXsd.Models;
using Microsoft.Extensions.Logging;
using static DevToys.Api.GUI;

namespace DevToys.XmlXsd;

[Export(typeof(IGuiTool))]
[Name("XmlXsdConverter")]                                                         // A unique, internal name of the tool.
[ToolDisplayInformation(
    IconFontName = "DevToys-Tools-Icons",                                       // This font is available by default in DevToys
    IconGlyph = '\u0122',                                                     // An icon that represents a pizza
    GroupName = PredefinedCommonToolGroupNames.Converters,                    // The group in which the tool will appear in the side bar.
    ResourceManagerAssemblyIdentifier = nameof(XmlXsdAssemblyIdentifier), // The Resource Assembly Identifier to use
    ResourceManagerBaseName = "DevToys.XmlXsd.XsdGenerator",                      // The full name (including namespace) of the resource file containing our localized texts
    ShortDisplayTitleResourceName = nameof(XsdGenerator.ShortDisplayTitle),    // The name of the resource to use for the short display title
    LongDisplayTitleResourceName = nameof(XsdGenerator.LongDisplayTitle),
    DescriptionResourceName = nameof(XsdGenerator.Description),
    AccessibleNameResourceName = nameof(XsdGenerator.AccessibleName))]
internal sealed class XmlXsdConverterGuiTool : IGuiTool
{
    private enum GridColumn
    {
        Content
    }

    private enum GridRow
    {
        Header,
        Content,
        Footer
    }

    private const string _xmlLanguage = "xml";
    private const string _xsdLanguage = "xsd";
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly DisposableSemaphore _semaphore = new();
    internal Task? WorkTask { get; private set; }

    /// <summary>
    /// Whether the tool should convert Xml to Xsd or Xsd to Xml.
    /// </summary>
    private static readonly SettingDefinition<XmlToXsdConversion> _conversionMode
        = new(name: $"{nameof(XmlXsdConverterGuiTool)}.{nameof(_conversionMode)}", defaultValue: XmlToXsdConversion.XmlToXsd);

    /// <summary>
    /// Which indentation the tool need to use.
    /// </summary>
    private static readonly SettingDefinition<Indentation> _indentationMode
        = new(name: $"{nameof(XmlXsdConverterGuiTool)}.{nameof(_indentationMode)}", defaultValue: Indentation.TwoSpaces);

    private readonly IUIMultiLineTextInput _outputTextArea = MultiLineTextInput("xml-to-xsd-output-text-area")
        .Title("XSD")
        .Language("xml")
        .CanCopyWhenEditable()
        .Extendable();

    private readonly IUIMultiLineTextInput _inputTextArea = MultiLineTextInput("xml-to-xsd-input-text-area")
        .Title("XML")
        .Language("xml")
        .CanCopyWhenEditable()
        .Extendable();

    [method: ImportingConstructor]
    public XmlXsdConverterGuiTool(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
        _logger = this.Log();

        switch (_settingsProvider.GetSetting(_conversionMode))
        {
            case XmlToXsdConversion.XmlToXsd:
                SetXmlToXsdConversion();
                break;
            case XmlToXsdConversion.XsdToXml:
                SetXsdToXmlConversion();
                break;
            default:
                throw new NotSupportedException();
        }
    }

    public UIToolView View
        => new(
            isScrollable: true,
            Grid()
                .ColumnLargeSpacing()
                .RowLargeSpacing()
                .Rows(
                    (GridRow.Header, Auto),
                    (GridRow.Content, new UIGridLength(1, UIGridUnitType.Fraction))
                )
                .Columns(
                    (GridColumn.Content, new UIGridLength(1, UIGridUnitType.Fraction))
                )
            .Cells(
                Cell(
                    GridRow.Header,
                    GridColumn.Content,
                    Stack().Vertical().WithChildren(
                        Label()
                        .Text(XsdGenerator.Configuration),
                        Setting("xml-to-xsd-text-conversion-setting")
                        .Icon("FluentSystemIcons", '\uF18D')
                        .Title(XsdGenerator.ConversionTitle)
                        .Description(XsdGenerator.ConversionDescription)
                        .Handle(
                            _settingsProvider,
                            _conversionMode,
                            OnConversionModeChanged,
                            Item(XsdGenerator.XmlToXsd, XmlToXsdConversion.XmlToXsd),
                            Item(XsdGenerator.XsdToXml, XmlToXsdConversion.XsdToXml)
                        ),
                        Setting("xml-to-xsd-text-indentation-setting")
                        .Icon("FluentSystemIcons", '\uF6F8')
                        .Title(XsdGenerator.Indentation)
                        .Handle(
                            _settingsProvider,
                            _indentationMode,
                            OnIndentationModelChanged,
                            Item(XsdGenerator.TwoSpaces, Indentation.TwoSpaces),
                            Item(XsdGenerator.FourSpaces, Indentation.FourSpaces),
                            Item(XsdGenerator.Tab, Indentation.OneTab)
                        )
                    )
                ),
                Cell(
                    GridRow.Content,
                    GridColumn.Content,
                    SplitGrid()
                        .Vertical()
                        .WithLeftPaneChild(
                            _inputTextArea
                                .Title(XsdGenerator.Input)
                                .OnTextChanged(OnInputTextChanged))
                        .WithRightPaneChild(
                            _outputTextArea
                                .Title(XsdGenerator.Output)
                                .ReadOnly()
                                .Extendable())
                )
            )
        );

    private void OnInputTextChanged(string text)
    {
        StartConvert(text);
    }

    private void OnIndentationModelChanged(Indentation obj)
    {
        StartConvert(_inputTextArea.Text);
    }

    private void OnConversionModeChanged(XmlToXsdConversion conversionMode)
    {
        switch (conversionMode)
        {
            case XmlToXsdConversion.XmlToXsd:
                SetXmlToXsdConversion();
                break;
            case XmlToXsdConversion.XsdToXml:
                SetXsdToXmlConversion();
                break;
            default:
                throw new NotSupportedException();
        }

        _inputTextArea.Text(_outputTextArea.Text);
    }

    public void OnDataReceived(string dataTypeName, object? parsedData)
    {
        if (dataTypeName == PredefinedCommonDataTypeNames.Xml &&
            parsedData is string xmlStrongTypedParsedData)
        {
            _inputTextArea.Language(_xmlLanguage);
            _outputTextArea.Language(_xsdLanguage);
            _settingsProvider.SetSetting(_conversionMode, XmlToXsdConversion.XmlToXsd);
            _inputTextArea.Text(xmlStrongTypedParsedData);
        }

        if (dataTypeName == PredefinedCommonDataTypeNames.Xsd &&
            parsedData is string xsdStrongTypedParsedData)
        {
            _inputTextArea.Language(_xsdLanguage);
            _outputTextArea.Language(_xmlLanguage);
            _settingsProvider.SetSetting(_conversionMode, XmlToXsdConversion.XsdToXml);
            _inputTextArea.Text(xsdStrongTypedParsedData);
        }
    }

    private void StartConvert(string text)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        WorkTask = ConvertAsync(text, _settingsProvider.GetSetting(_conversionMode), _settingsProvider.GetSetting(_indentationMode), _cancellationTokenSource.Token);
    }

    private async Task ConvertAsync(string input, XmlToXsdConversion conversionModeSetting, Indentation indentationModeSetting, CancellationToken cancellationToken)
    {
        using (await _semaphore.WaitAsync(cancellationToken))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(cancellationToken);

            ResultInfo<string> conversionResult = await XmlXsdHelper.ConvertAsync(
                input,
                conversionModeSetting,
                indentationModeSetting,
                _logger,
                cancellationToken);
            _outputTextArea.Text(conversionResult.Data!);
        }
    }

    private void SetXmlToXsdConversion()
    {
        _inputTextArea
            .Language(_xmlLanguage);
        _outputTextArea
            .Language(_xmlLanguage);
    }

    private void SetXsdToXmlConversion()
    {
        _inputTextArea
            .Language(_xmlLanguage);
        _outputTextArea
            .Language(_xmlLanguage);
    }
}