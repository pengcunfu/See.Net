using System.Text;

namespace See.ViewModels;

public sealed record EncodingOption(string DisplayName, Encoding Encoding, bool WriteBom)
{
    public override string ToString() => DisplayName;
}
