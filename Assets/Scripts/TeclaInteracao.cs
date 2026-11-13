using TMPro;
using UnityEngine;

// O ícone ocupa um caractere na linha, inclusive quando o aviso quebra em duas linhas.
public sealed class TeclaInteracao : ITextPreprocessor
{
    private const string Icone = "<sprite=0>";
    private static readonly TeclaInteracao formatador = new TeclaInteracao();

    public static void Aplicar(TMP_Text texto)
    {
        if (texto == null) return;
        var tecla = Resources.Load<TMP_SpriteAsset>("TeclaInteracao");
        if (tecla == null) return;
        texto.spriteAsset = tecla;
        texto.textPreprocessor = formatador;
        texto.havePropertiesChanged = true;
    }

    public string PreprocessText(string texto)
    {
        if (texto == "E") return Icone;
        if (texto != null && texto.StartsWith("[E]", System.StringComparison.Ordinal))
            return Icone + texto.Substring(3);
        return texto;
    }
}
