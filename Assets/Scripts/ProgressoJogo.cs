using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

[Serializable]
public class DadosJogo
{
    public int versao;
    public string jogo, cena, salvoEmUtc;
    public float x, y, z, direcao;
    public float vida;
    public int paginaDiario;
    public bool temDiario, chefeVencido, bauDiarioAberto, diarioLido;
    public float chefeX, chefeY, chefeZ;
    public string[] alavancas, baus;

    public bool Valido()
    {
        return versao == 1 && jogo == "DeadEndy" && ProgressoJogo.EhCenaJogo(cena) &&
            DateTime.TryParse(salvoEmUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out _) &&
            Finito(x) && Finito(y) && Finito(z) && Finito(direcao) &&
            Finito(chefeX) && Finito(chefeY) && Finito(chefeZ) &&
            Finito(vida) && vida > 0 && vida <= 10000 && paginaDiario >= 0 && paginaDiario <= 10000 &&
            (!temDiario || (chefeVencido && bauDiarioAberto)) &&
            (!diarioLido || temDiario) &&
            (!bauDiarioAberto || chefeVencido) && ListaValida(alavancas) && ListaValida(baus) &&
            (chefeVencido || baus.Length == 0);
    }
    static bool Finito(float n) => !float.IsNaN(n) && !float.IsInfinity(n) && Math.Abs(n) < 1000000;
    static bool ListaValida(string[] lista) => lista != null && lista.Length <= 1000 &&
        Array.TrueForAll(lista, s => !string.IsNullOrWhiteSpace(s) && s.Length <= 512);
}

public static class ProgressoJogo
{
    public const string CenaJogo = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/Demo_Day.unity";
    public const string CenaIntegrada = "Assets/Cenario/PNB_Tropical_Jungle/Scenes/juncao_fase1-2.unity";
    public static bool EhCenaJogo(string caminho) => caminho == CenaJogo || caminho == CenaIntegrada;
    public const string CenaMenu = "Assets/MenuPrincipal.unity";
    public enum Entrada { Automatica, NovoJogo, Continuar }
    public static Entrada ProximaEntrada { get; private set; }
    static DadosJogo carregamento;
    public static string Caminho => Path.Combine(Application.persistentDataPath,
        Application.isEditor ? "autosave-editor.json" : "autosave.json");
    public static bool ExisteArquivo => new ArquivoSave(Caminho).Existe;

    [Serializable] class Registro { public string dados, sha256; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ReiniciarEstado() { ProximaEntrada = Entrada.Automatica; carregamento = null; }

    static string Hash(string texto)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(texto)));
    }

    public static bool Decodificar(string texto, out DadosJogo dados)
    {
        dados = null;
        try
        {
            var registro = JsonUtility.FromJson<Registro>(texto);
            if (registro == null || string.IsNullOrEmpty(registro.dados) || registro.sha256 != Hash(registro.dados)) return false;
            var candidato = JsonUtility.FromJson<DadosJogo>(registro.dados);
            if (candidato == null || !candidato.Valido()) return false;
            dados = candidato;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    public static bool TentarCarregar(out DadosJogo dados, out string aviso)
    {
        dados = null;
        var arquivo = new ArquivoSave(Caminho);
        if (!arquivo.Ler(t => Decodificar(t, out _), out string texto, out bool backup, out aviso)) return false;
        if (!Decodificar(texto, out dados)) return false;
        aviso = backup ? "O último arquivo estava indisponível. Recuperamos o checkpoint de segurança." : null;
        return true;
    }

    public static bool Salvar(DadosJogo dados, bool novaPartida, out string erro)
    {
        if (dados == null || !dados.Valido()) { erro = "Checkpoint inválido."; return false; }
        string json = JsonUtility.ToJson(dados);
        string registro = JsonUtility.ToJson(new Registro { dados = json, sha256 = Hash(json) }, true);
        return new ArquivoSave(Caminho).Gravar(registro, t => Decodificar(t, out _), novaPartida, out erro);
    }

    public static void PrepararNovoJogo() { ProximaEntrada = Entrada.NovoJogo; carregamento = null; }
    public static bool PrepararContinuar(out string aviso)
    {
        if (!TentarCarregar(out var dados, out aviso)) return false;
        ProximaEntrada = Entrada.Continuar;
        carregamento = dados;
        return true;
    }
    public static DadosJogo ConsumirEntrada(out bool novoJogo, out string aviso)
    {
        novoJogo = ProximaEntrada == Entrada.NovoJogo;
        aviso = null;
        var dados = carregamento;
        if (!novoJogo && dados == null) TentarCarregar(out dados, out aviso);
        ReiniciarEstado();
        return novoJogo ? null : dados;
    }
}
