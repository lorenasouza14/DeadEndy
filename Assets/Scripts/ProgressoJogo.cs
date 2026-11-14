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
    // Ausente nos saves antigos: a fase do gorila comeca no estado original.
    public DadosGorila gorila;

    public bool Valido()
    {
        return (versao == 1 || versao == 2) && jogo == "DeadEndy" && ProgressoJogo.EhCenaJogo(cena) &&
            DateTime.TryParse(salvoEmUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out _) &&
            Finito(x) && Finito(y) && Finito(z) && Finito(direcao) &&
            Finito(chefeX) && Finito(chefeY) && Finito(chefeZ) &&
            Finito(vida) && vida > 0 && vida <= 10000 && paginaDiario >= 0 && paginaDiario <= 10000 &&
            (!temDiario || (chefeVencido && bauDiarioAberto)) &&
            (!diarioLido || temDiario) &&
            (!bauDiarioAberto || chefeVencido) && ListaValida(alavancas) && ListaValida(baus) &&
            (chefeVencido || baus.Length == 0) &&
            (versao == 1 || (gorila != null && gorila.Valido()));
    }
    static bool Finito(float n) => !float.IsNaN(n) && !float.IsInfinity(n) && Math.Abs(n) < 1000000;
    static bool ListaValida(string[] lista) => lista != null && lista.Length <= 1000 &&
        Array.TrueForAll(lista, s => !string.IsNullOrWhiteSpace(s) && s.Length <= 512);
}

[Serializable]
public class DadosGorila
{
    public string bananaNaMao;
    public DadosRuinas[] ruinas;
    public DadosSopa[] sopas;

    public bool Valido()
    {
        if ((!string.IsNullOrEmpty(bananaNaMao) && !IdValido(bananaNaMao)) ||
            ruinas == null || sopas == null || ruinas.Length > 100 || sopas.Length > 100) return false;
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var r in ruinas)
            if (r == null || !IdValido(r.id) || !ids.Add(r.id) || r.passos == null || r.passos.Length != 3 ||
                !Array.TrueForAll(r.passos, p => p >= 0 && p <= 3)) return false;
        ids.Clear();
        var bananas = new System.Collections.Generic.HashSet<string>();
        foreach (var s in sopas)
        {
            if (s == null || !IdValido(s.id) || !ids.Add(s.id) || s.bananas == null || s.bananas.Length > 3 ||
                (s.passagemLiberada && s.bananas.Length != 3)) return false;
            foreach (var banana in s.bananas)
                if (!IdValido(banana) || !bananas.Add(banana) || banana == bananaNaMao) return false;
        }
        return true;
    }

    static bool IdValido(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 512;
}

[Serializable]
public class DadosRuinas
{
    public string id;
    public int[] passos;
    public bool resolvido;
}

[Serializable]
public class DadosSopa
{
    public string id;
    public string[] bananas;
    public bool passagemLiberada;
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
            // JsonUtility pode criar um objeto vazio para um campo ausente no JSON.
            // A versao 1 nunca registrou o gorila; descarte esse objeto na migracao.
            if (candidato.versao == 1) candidato.gorila = null;
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
