namespace TecnmInventario.Api.Features.Documents;

/// <summary>
/// ST4: el nombre que manda el cliente es solo un dato para mostrar y para
/// Content-Disposition; nunca participa en la ruta en disco.
/// </summary>
public static class DocumentFileNames
{
    public const int MaxLength = Common.Domain.DbFieldLengths.Documents.FileName;

    /// <summary>
    /// Se queda con lo que va tras el último separador ("/" o "\"), quita los
    /// caracteres de control y no deja nombres vacíos ni "." / "..".
    /// </summary>
    public static string Sanitize(string? clientName, string extension)
    {
        var name = clientName ?? string.Empty;

        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (name.Length == 0 || name is "." or "..")
        {
            name = $"archivo{extension}";
        }

        return name.Length > MaxLength ? name[..MaxLength] : name;
    }
}
