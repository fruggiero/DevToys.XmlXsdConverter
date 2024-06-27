using System.Xml.Schema;
using System.Xml;

using DevToys.Api;
using DevToys.XmlXsd.Models;
using Microsoft.Extensions.Logging;

namespace DevToys.XmlXsd;

internal static class XsdHelper
{
    internal static ResultInfo<string> ConvertToXml(
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
            // Crea un XmlSchemaSet e aggiungi lo schema XSD
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            using (StringReader stringReader = new StringReader(input))
            {
                using (XmlReader xmlReader = XmlReader.Create(stringReader))
                {
                    schemaSet.Add(null, xmlReader);
                }
            }

            // Compila lo schema per verificare che sia valido
            schemaSet.Compile();

            // Genera l'XML di esempio
            StringWriter stringWriter = new StringWriter();
            using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true }))
            {
                foreach (XmlSchema schema in schemaSet.Schemas())
                {
                    foreach (XmlSchemaElement element in schema.Elements.Values)
                    {
                        WriteElement(xmlWriter, element);
                    }
                }
            }

            // Ottieni la stringa XML
            cancellationToken.ThrowIfCancellationRequested();

            // Output
            return new(stringWriter.ToString(), true);
        }
        catch (XmlSchemaException ex)
        {
            return new(ex.Message, false);
        }
        catch (OperationCanceledException)
        {
            return new(string.Empty, false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Xsd to Xml Converter");
            return new(string.Empty, false);
        }
    }

    static void WriteElement(XmlWriter writer, XmlSchemaElement element)
    {
        writer.WriteStartElement(element.Name);

        if (element.ElementSchemaType is XmlSchemaComplexType complexType)
        {
            WriteComplexType(writer, complexType);
        }
        else if (element.ElementSchemaType is XmlSchemaSimpleType)
        {
            writer.WriteString("sampleValue");
        }

        writer.WriteEndElement();
    }

    static void WriteComplexType(XmlWriter writer, XmlSchemaComplexType complexType)
    {
        if (complexType.ContentTypeParticle is XmlSchemaSequence sequence)
        {
            foreach (var item in sequence.Items)
            {
                if (item is XmlSchemaElement childElement)
                {
                    WriteElement(writer, childElement);
                }
                else if (item is XmlSchemaAny)
                {
                    writer.WriteStartElement("anyElement");
                    writer.WriteString("sampleValue");
                    writer.WriteEndElement();
                }
                else if (item is XmlSchemaChoice choice)
                {
                    foreach (var choiceItem in choice.Items)
                    {
                        if (choiceItem is XmlSchemaElement choiceElement)
                        {
                            WriteElement(writer, choiceElement);
                        }
                        else if (choiceItem is XmlSchemaAny)
                        {
                            writer.WriteStartElement("anyElement");
                            writer.WriteString("sampleValue");
                            writer.WriteEndElement();
                        }
                    }
                }
            }
        }
    }
}