using System.Text;

namespace RagBasico.Core.Chunking;

public sealed class Chunker
{
    // Jerarquía de separadores, del más "grande" (respeta más sentido) al más chico.
    // Solo se baja a un separador más agresivo si el fragmento sigue siendo muy grande.
    private static readonly string[] Separators = ["\n\n", "\n", ". ", " "];

    // chunkSize y overlap se miden en caracteres, no en párrafos.
    public IReadOnlyList<TextChunk> Split(string text, int chunkSize = 500, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<TextChunk>();

        if (chunkSize <= 0)
            throw new ArgumentException("El tamaño del chunk debe ser mayor a 0.", nameof(chunkSize));

        if (overlap < 0 || overlap >= chunkSize)
            throw new ArgumentException("El overlap debe ser mayor o igual a 0 y menor que chunkSize.", nameof(overlap));

        var pieces = SplitRecursive(text.Trim(), chunkSize, separatorIndex: 0);
        var merged = MergeWithOverlap(pieces, chunkSize, overlap);

        var chunks = new List<TextChunk>(merged.Count);
        for (int i = 0; i < merged.Count; i++)
            chunks.Add(new TextChunk(i, merged[i]));

        return chunks;
    }

    // Parte el texto usando el separador actual. Cualquier fragmento que siga siendo
    // más grande que chunkSize se vuelve a partir con el siguiente separador de la lista.
    // Si ya no quedan separadores, el fragmento se deja como está (ver nota de límite abajo).
    private static List<string> SplitRecursive(string text, int chunkSize, int separatorIndex)
    {
        if (text.Length <= chunkSize || separatorIndex >= Separators.Length)
            return [text];

        var rawPieces = text.Split(Separators[separatorIndex], StringSplitOptions.RemoveEmptyEntries);

        var result = new List<string>();
        foreach (var piece in rawPieces)
        {
            var trimmed = piece.Trim();
            if (trimmed.Length == 0)
                continue;

            if (trimmed.Length > chunkSize)
                result.AddRange(SplitRecursive(trimmed, chunkSize, separatorIndex + 1));
            else
                result.Add(trimmed);
        }

        // Si el separador no partió nada útil (ej. no aparece en el texto), evita perder el fragmento.
        return result.Count > 0 ? result : [text];
    }

    // Agrupa fragmentos pequeños en chunks de hasta chunkSize caracteres.
    // Al cerrar un chunk, arrastra los últimos 'overlap' caracteres al siguiente.
    private static List<string> MergeWithOverlap(List<string> pieces, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var piece in pieces)
        {
            if (current.Length > 0 && current.Length + 1 + piece.Length > chunkSize)
            {
                chunks.Add(current.ToString());

                var tail = TakeTail(current.ToString(), overlap);
                current.Clear();
                current.Append(tail);
            }

            if (current.Length > 0)
                current.Append(' ');

            current.Append(piece);
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks;
    }

    private static string TakeTail(string text, int length)
    {
        if (length <= 0)
            return string.Empty;

        return text.Length <= length ? text : text[^length..]; // text[^length..] comienza length posiciones desde el final.
    }
}
