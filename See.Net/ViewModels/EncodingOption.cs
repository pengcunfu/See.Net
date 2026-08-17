using System.Text;

namespace See.Net.ViewModels;

public sealed record EncodingOption(string DisplayName, Encoding Encoding, bool WriteBom)
{
    public override string ToString() => DisplayName;
}
