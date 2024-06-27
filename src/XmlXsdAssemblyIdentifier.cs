using DevToys.Api;
using System.ComponentModel.Composition;

namespace DevToys.XmlXsd
{
    [Export(typeof(IResourceAssemblyIdentifier))]
    [Name(nameof(XmlXsdAssemblyIdentifier))]
    internal sealed class XmlXsdAssemblyIdentifier : IResourceAssemblyIdentifier
    {
        public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
        {
            return new([]);
        }
    }
}