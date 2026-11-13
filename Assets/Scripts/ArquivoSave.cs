using System;
using System.IO;
using System.Text;

// Sem dependências do Unity: a gravação pode ser verificada usando uma pasta temporária.
public sealed class ArquivoSave
{
    public string Caminho { get; }
    public string Backup => Caminho + ".bak";
    public bool Existe => File.Exists(Caminho) || File.Exists(Backup);
    const int LimiteBytes = 1024 * 1024;

    public ArquivoSave(string caminho) { Caminho = caminho; }

    public bool Ler(Func<string, bool> validar, out string conteudo, out bool usouBackup, out string erro)
    {
        usouBackup = false;
        if (LerArquivo(Caminho, validar, out conteudo, out erro)) return true;
        string erroPrincipal = erro;
        if (LerArquivo(Backup, validar, out conteudo, out erro)) { usouBackup = true; erro = null; return true; }
        erro = Existe ? "Não foi possível ler o progresso salvo. " + erroPrincipal : null;
        return false;
    }

    static bool LerArquivo(string caminho, Func<string, bool> validar, out string conteudo, out string erro)
    {
        conteudo = null;
        erro = null;
        try
        {
            if (!File.Exists(caminho)) return false;
            if (new FileInfo(caminho).Length > LimiteBytes) { erro = "Arquivo muito grande."; return false; }
            string texto = File.ReadAllText(caminho, Encoding.UTF8);
            if (!validar(texto)) { erro = "Arquivo inválido ou de outra versão."; return false; }
            conteudo = texto;
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        { erro = e.Message; return false; }
    }

    public bool Gravar(string conteudo, Func<string, bool> validar, bool novaPartida, out string erro)
    {
        erro = null;
        string temporario = Caminho + ".tmp";
        try
        {
            if (string.IsNullOrEmpty(conteudo) || Encoding.UTF8.GetByteCount(conteudo) > LimiteBytes || !validar(conteudo))
            { erro = "O progresso gerado não é válido."; return false; }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Caminho)));
            // Flush(true) termina a escrita antes de substituir o checkpoint anterior.
            using (var arquivo = new FileStream(temporario, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(conteudo);
                arquivo.Write(bytes, 0, bytes.Length);
                arquivo.Flush(true);
            }
            if (File.Exists(Caminho))
            {
                bool principalValido = LerArquivo(Caminho, validar, out _, out _);
                // Um principal corrompido nunca substitui o backup saudável.
                File.Replace(temporario, Caminho, principalValido && !novaPartida ? Backup : null);
            }
            else File.Move(temporario, Caminho);
            // Nunca recuperar uma partida antiga depois de confirmar Novo Jogo.
            if (novaPartida && File.Exists(Backup)) File.Delete(Backup);
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
        { erro = e.Message; return false; }
        finally
        {
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
