using DevToys.Api;
using DevToys.XmlXsd.Models;
using Microsoft.Extensions.Logging;
using System.Xml;
using System.Xml.Schema;
using DevToys.XmlXsd.Extensions;

namespace DevToys.XmlXsd;

internal static class XmlHelper
{
    internal static ResultInfo<string> ConvertToXsd(
        string? input,
        Indentation indentationMode,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new(string.Empty, false);
        }

        try
        {
            using var stringReader = new StringReader(input);
            using var xmlReader = XmlReader.Create(stringReader);

            // Infer schema
            var schemaSet = new XmlSchemaInference().InferSchema(xmlReader);

            // Set output settings
            var outputSettings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = indentationMode.ToIndentChars(),
                NewLineOnAttributes = false
            };

            // Write to string
            using var stringWriter = new StringWriter();
            using (var xmlWriter = XmlWriter.Create(stringWriter, outputSettings))
            {
                foreach (XmlSchema schema in schemaSet.Schemas())
                {
                    schema.Write(xmlWriter);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Output
            return new(stringWriter.ToString());
        }
        catch (XmlException ex)
        {
            return new(ex.Message, false);
        }
        catch (OperationCanceledException)
        {
            return new(string.Empty, false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Xml to Xsd Converter");
            return new(string.Empty, false);
        }
    }
}