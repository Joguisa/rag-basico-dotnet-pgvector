using System.Text; // StringBuilder: para armar cada chunk sin crear un string nuevo en cada concatenación

namespace RagBasico.Core.Chunking;

public sealed class Chunker
{
    // Jerarquía de separadores, del más "grande" (respeta más sentido) al más chico.
    // Solo se baja a un separador más agresivo si el fragmento sigue siendo muy grande.
    private static readonly string[] Separators = ["\n\n", "\n", ". ", " "]; // orden de intento: párrafo -> línea -> oración -> palabra

    // chunkSize y overlap se miden en caracteres, no en párrafos.
    public IReadOnlyList<TextChunk> Split(string text, int chunkSize, int overlap) // punto de entrada público de la clase
    {
        if (string.IsNullOrWhiteSpace(text)) // texto vacío o solo espacios: no hay nada que trocear
            return []; // lista vacía, nunca null (el caller no tiene que chequear null)

        if (chunkSize <= 0) // un chunk de tamaño 0 o negativo no tiene sentido
            throw new ArgumentException("El tamaño del chunk debe ser mayor a 0.", nameof(chunkSize));

        if (overlap < 0 || overlap >= chunkSize) // el overlap no puede ser negativo ni "comerse" el chunk entero
            throw new ArgumentException("El overlap debe ser mayor o igual a 0 y menor que chunkSize.", nameof(overlap));

        var pieces = SplitRecursive(text.Trim(), chunkSize, separatorIndex: 0); // fase 1: partir en fragmentos <= chunkSize respetando separadores naturales
        var merged = MergeWithOverlap(pieces, chunkSize, overlap); // fase 2: reagrupar esos fragmentos en chunks finales, con solapamiento entre ellos

        var chunks = new List<TextChunk>(merged.Count); // capacidad inicial conocida: evita resize interno de la lista
        for (int i = 0; i < merged.Count; i++) // recorre en orden para que el índice refleje la posición real del chunk
            chunks.Add(new TextChunk(i, merged[i])); // el índice queda "pegado" al chunk (se usa como chunk_index en la base)

        return chunks; // lista final, lista para pasar al pipeline de embeddings
    }

    // Parte el texto usando el separador actual. Cualquier fragmento que siga siendo
    // más grande que chunkSize se vuelve a partir con el siguiente separador de la lista.
    // Si ya no quedan separadores, se corta a lo bruto por caracteres (HardSplit) para
    // garantizar que ninguna pieza supere chunkSize.
    private static List<string> SplitRecursive(string text, int chunkSize, int separatorIndex) // recursiva: cada llamada prueba UN separador
    {
        if (text.Length <= chunkSize) // caso base 1: ya entra entero, no hace falta partir más
            return [text];

        if (separatorIndex >= Separators.Length) // caso base 2: se acabaron los separadores y sigue siendo grande
            return HardSplit(text, chunkSize); // último recurso: cortar a lo bruto por caracteres

        var separator = Separators[separatorIndex]; // separador que se prueba en esta llamada
        var rawPieces = text.Split(separator, StringSplitOptions.RemoveEmptyEntries); // parte el texto; RemoveEmptyEntries evita fragmentos "" por separadores consecutivos

        var result = new List<string>(); // piezas ya procesadas en este nivel de recursión
        for (int i = 0; i < rawPieces.Length; i++) // índice explícito (no foreach) porque hace falta saber cuál es la última pieza
        {
            var trimmed = rawPieces[i].Trim(); // saca espacios/saltos de línea sobrantes en los bordes
            if (trimmed.Length == 0) // pieza vacía después de trim (ej. separador pegado al borde del texto): se descarta
                continue;

            // Split() se come el separador. Para ". " eso borra el punto entre oraciones;
            // se lo devolvemos a cada pieza que no sea la última (a la última no le faltó
            // nada, porque no había separador después de ella).
            var isLastPiece = i == rawPieces.Length - 1; // solo la última pieza conserva intacto lo que tenía después
            if (separator == ". " && !isLastPiece) // caso especial: reponer el punto que el Split() se comió
                trimmed += ".";

            if (trimmed.Length > chunkSize) // esta pieza todavía es muy grande
                result.AddRange(SplitRecursive(trimmed, chunkSize, separatorIndex + 1)); // recursión: probar el siguiente separador, más agresivo
            else
                result.Add(trimmed); // ya entra en un chunk, se guarda tal cual
        }

        // Si el separador no partió nada útil (ej. no aparece en el texto), probamos
        // con el siguiente separador en vez de devolver el texto tal cual sobredimensionado.
        return result.Count > 0 ? result : SplitRecursive(text, chunkSize, separatorIndex + 1); // red de seguridad: nunca devolver una pieza sin intentar reducirla
    }

    // Último recurso: corta el texto cada chunkSize caracteres, sin respetar palabras.
    // Solo se llega acá cuando ya no quedan separadores y la pieza sigue siendo más
    // grande que chunkSize (ej. una URL larga o un bloque de texto sin espacios).
    private static List<string> HardSplit(string text, int chunkSize) // garantiza el límite duro de tamaño, a costa de poder partir una palabra
    {
        var result = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize) // avanza de a chunkSize caracteres
            result.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i))); // Math.Min evita pasarse del final del string en el último tramo

        return result;
    }

    // Agrupa fragmentos pequeños en chunks de hasta ~chunkSize caracteres.
    // Al cerrar un chunk, arrastra los últimos 'overlap' caracteres al siguiente.
    //
    // Nota: un chunk puntual puede superar chunkSize en casos borde, cuando el overlap
    // heredado más una pieza que ya venía al límite no entran juntos. Es preferible a
    // partir una pieza a la mitad (rompería palabras) o a emitir un chunk que sea
    // únicamente el overlap sin contenido nuevo.
    private static List<string> MergeWithOverlap(List<string> pieces, int chunkSize, int overlap) // fase 2: arma los chunks finales
    {
        var chunks = new List<string>(); // chunks ya cerrados y listos
        var current = new StringBuilder(); // chunk que se está armando ahora mismo
        var newContentLength = 0; // caracteres agregados desde el último cierre, sin contar el overlap heredado

        foreach (var piece in pieces) // recorre las piezas en el orden en que aparecen en el texto original
        {
            var separatorLength = current.Length > 0 ? 1 : 0; // el espacio que se insertaría antes de la pieza, si current ya tiene algo

            /* Solo cerramos si el chunk actual ya tiene contenido nuevo (newContentLength > 0).
               Si cerráramos con newContentLength == 0, current sería puro overlap heredado
               y emitiríamos un chunk "basura" sin nada nuevo. */
            if (newContentLength > 0 && current.Length + separatorLength + piece.Length > chunkSize) // ¿agregar esta pieza desborda el chunk actual?
            {
                chunks.Add(current.ToString()); // cierra el chunk actual tal como está

                var tail = TakeTail(current.ToString(), overlap); // guarda los últimos 'overlap' caracteres como puente al próximo chunk
                current.Clear(); // vacía el builder para empezar el chunk siguiente
                current.Append(tail); // el chunk siguiente arranca con el overlap del anterior
                newContentLength = 0; // resetea: lo que hay en current ahora es solo overlap heredado, todavía no hay contenido nuevo
            }

            if (current.Length > 0) // si current ya tiene algo (texto previo u overlap heredado)
                current.Append(' '); // separa con un espacio antes de pegar la próxima pieza

            current.Append(piece); // agrega la pieza al chunk en construcción
            newContentLength += piece.Length; // cuenta esta pieza como contenido nuevo del chunk actual
        }

        if (newContentLength > 0) // queda contenido nuevo sin cerrar (el último chunk, que el loop nunca llega a cerrar)
            chunks.Add(current.ToString());

        return chunks;
    }

    private static string TakeTail(string text, int length) // devuelve los últimos 'length' caracteres de un string
    {
        if (length <= 0) // overlap 0: no hay nada que arrastrar
            return string.Empty;

        return text.Length <= length ? text : text[^length..]; // text[^length..] comienza length posiciones desde el final.
    }
}
