using DevToys.Api;
using DevToys.XmlXsd.Models;
using Microsoft.Extensions.Logging;

namespace DevToys.XmlXsd;

internal static class Conversion
{
    public static async ValueTask<ResultInfo<string>> ConvertAsync(
        string input,
        XmlToXsdConversion conversion,
        Indentation indentation,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(cancellationToken);

        ResultInfo<string> conversionResult;
        switch (conversion)
        {
            case XmlToXsdConversion.XmlToXsd:
                conversionResult = XmlHelper.ConvertToXsd(input, indentation, logger, cancellationToken);
                if (!conversionResult.HasSucceeded && string.IsNullOrWhiteSpace(conversionResult.Data))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new(Localization.XsdGenerator.InvalidXml, false);
                }
                break;
            case XmlToXsdConversion.XsdToXml:
                conversionResult = XsdHelper.ConvertToXml(input, indentation, logger, cancellationToken);
                if (!conversionResult.HasSucceeded && string.IsNullOrWhiteSpace(conversionResult.Data))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new(Localization.XsdGenerator.InvalidXsd, false);
                }
                break;
            default:
                throw new NotSupportedException();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return conversionResult;
    }
}
