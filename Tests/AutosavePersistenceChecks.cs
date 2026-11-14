using System;
using System.IO;

// Executado fora do Unity contra as classes reais, com saves numa pasta de teste.
public static class AutosavePersistenceChecks
{
    static int aprovados;
    static void Check(bool condicao, string nome)
    {
        if (!condicao) throw new Exception("FAIL: " + nome);
        Console.WriteLine("PASS: " + nome);
        aprovados++;
    }
    static bool Valido(string texto) => texto.StartsWith("checkpoint:") && texto.EndsWith(":completo");
    static string Registro(int n) => "checkpoint:" + n + ":completo";

    public static int Main(string[] args)
    {
        string pasta = Path.Combine(args[0], Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var arquivo = new ArquivoSave(Path.Combine(pasta, "autosave.json"));
        Check(!arquivo.Ler(Valido, out _, out _, out _) && !arquivo.Existe, "Sem arquivo não há checkpoint para continuar");
        Check(arquivo.Gravar(Registro(1), Valido, true, out _), "Primeiro save cria arquivo e pasta");
        Check(arquivo.Ler(Valido, out var texto, out var backup, out _) && texto == Registro(1) && !backup, "Leitura recupera último checkpoint");
        Check(arquivo.Gravar(Registro(2), Valido, false, out _) && File.ReadAllText(arquivo.Backup) == Registro(1), "Atualização guarda checkpoint anterior em backup");
        Check(!arquivo.Gravar("incompleto", Valido, false, out _) && File.ReadAllText(arquivo.Caminho) == Registro(2), "Gravação inválida não altera save existente");
        File.WriteAllText(arquivo.Caminho, "arquivo truncado");
        Check(arquivo.Ler(Valido, out texto, out backup, out _) && backup && texto == Registro(1), "Corrupção do principal recupera backup");
        Check(arquivo.Gravar(Registro(3), Valido, false, out _) && File.ReadAllText(arquivo.Backup) == Registro(1), "Reparar principal não sobrescreve backup saudável com corrupção");
        File.WriteAllText(arquivo.Caminho + ".tmp", "escrita interrompida");
        Check(arquivo.Ler(Valido, out texto, out backup, out _) && texto == Registro(3), "Interrupção com temporário não invalida checkpoint concluído");
        Check(arquivo.Gravar(Registro(4), Valido, true, out _) && !File.Exists(arquivo.Backup), "Novo Jogo elimina backup da partida anterior após salvar");
        for (int i = 5; i <= 30; i++)
            if (!arquivo.Gravar(Registro(i), Valido, false, out string falha) || File.ReadAllText(arquivo.Backup) != Registro(i - 1))
                throw new Exception("Rotação de backup falhou: " + i + ": " + falha);
        Check(true, "26 gravações sucessivas mantêm principal e backup consistentes");
        using (var trava = new FileStream(arquivo.Caminho, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(!arquivo.Gravar(Registro(31), Valido, false, out _), "Arquivo bloqueado informa falha sem sucesso falso");
        Check(File.ReadAllText(arquivo.Caminho) == Registro(30) && File.ReadAllText(arquivo.Backup) == Registro(29), "Falha de escrita preserva os dois checkpoints anteriores");
        File.WriteAllText(arquivo.Caminho, new string('x', 1024 * 1024 + 1));
        Check(arquivo.Ler(Valido, out texto, out backup, out _) && backup && texto == Registro(29), "Arquivo excessivo é rejeitado com recuperação do backup");
        File.WriteAllText(arquivo.Backup, "corrompido");
        Check(!arquivo.Ler(Valido, out _, out _, out string erro) && !string.IsNullOrEmpty(erro), "Dois arquivos inválidos geram erro explícito");
        string bloqueio = Path.Combine(pasta, "nao-e-pasta");
        File.WriteAllText(bloqueio, "arquivo");
        Check(!new ArquivoSave(Path.Combine(bloqueio, "save.json")).Gravar(Registro(1), Valido, true, out erro) && !string.IsNullOrEmpty(erro), "Falha ao criar pasta não é ocultada");

        var dados = new DadosJogo
        {
            versao = 1, jogo = "DeadEndy", cena = ProgressoJogo.CenaJogo,
            salvoEmUtc = DateTime.UtcNow.ToString("O"), vida = 5,
            alavancas = new string[0], baus = new string[0]
        };
        Check(dados.Valido(), "Checkpoint inicial completo é válido");
        dados.cena = ProgressoJogo.CenaIntegrada;
        Check(dados.Valido(), "Checkpoint aceita a cena unificada");
        dados.cena = ProgressoJogo.CenaJogo;
        Check(dados.Valido(), "Checkpoints anteriores da primeira fase continuam validos");
        Check(dados.gorila == null && dados.Valido(), "Save antigo sem dados do gorila continua valido");
        dados.versao = 2;
        dados.gorila = new DadosGorila { ruinas = new DadosRuinas[0], sopas = new DadosSopa[0] };
        Check(dados.Valido(), "Nova partida aceita progresso vazio do gorila");
        var sopa = new DadosSopa { id = "CaldeiraoSopa|Caldeirao", bananas = new[] { "BananaSopa|Armadilhas" } };
        dados.gorila.sopas = new[] { sopa };
        dados.gorila.bananaNaMao = "BananaSopa|Santuario";
        Check(dados.Valido(), "Aceita entrega parcial com outra banana na mao");
        dados.gorila.bananaNaMao = sopa.bananas[0];
        Check(!dados.Valido(), "Rejeita mesma banana entregue e na mao");
        dados.gorila.bananaNaMao = null;
        sopa.bananas = new[] { "BananaSopa|Armadilhas", "BananaSopa|Armadilhas" };
        Check(!dados.Valido(), "Rejeita entrega duplicada");
        sopa.bananas = new[] { "BananaSopa|Armadilhas", "BananaSopa|Ruinas" };
        sopa.passagemLiberada = true;
        Check(!dados.Valido(), "Passagem aberta exige tres bananas diferentes");
        sopa.bananas = new[] { "BananaSopa|Armadilhas", "BananaSopa|Ruinas", "BananaSopa|Santuario" };
        Check(dados.Valido(), "Aceita sopa completa e passagem liberada");
        var ruina = new DadosRuinas { id = "PuzzleAltaresRuinas|Ruinas", passos = new[] { 1, 2, 3 } };
        dados.gorila.ruinas = new[] { ruina };
        Check(dados.Valido(), "Aceita giros parciais do puzzle das ruinas");
        ruina.passos[0] = 4;
        Check(!dados.Valido(), "Rejeita giro de altar fora dos quatro estados");
        ruina.passos = new[] { 0, 0, 0 };
        ruina.resolvido = true;
        Check(dados.Valido(), "Aceita ruinas concluidas junto da sopa");
        dados.gorila.ruinas = new[] { ruina, ruina };
        Check(!dados.Valido(), "Rejeita identificadores de ruinas duplicados");
        dados.gorila.ruinas = null;
        Check(!dados.Valido(), "Rejeita progresso do gorila incompleto");
        dados.gorila = null;
        dados.versao = 1;
        dados.vida = 5f / 3f;
        Check(dados.Valido(), "Checkpoint preserva vida fracionada do dano do gorila e de quedas");
        dados.vida = 0.25f;
        Check(dados.Valido(), "Checkpoint aceita personagem vivo com menos de um ponto de vida");
        dados.vida = float.NaN;
        Check(!dados.Valido(), "Checkpoint rejeita vida NaN");
        dados.vida = float.PositiveInfinity;
        Check(!dados.Valido(), "Checkpoint rejeita vida infinita");
        dados.vida = 5;
        dados.diarioLido = true;
        Check(!dados.Valido(), "Leitura do diário exige ter coletado o diário");
        dados.diarioLido = false;
        dados.vida = 0; Check(!dados.Valido(), "Não aceita checkpoint com Endy morto"); dados.vida = 5;
        dados.x = float.NaN; Check(!dados.Valido(), "Não aceita posição inválida"); dados.x = 0;
        dados.versao = 99; Check(!dados.Valido(), "Não tenta carregar versão desconhecida"); dados.versao = 1;
        dados.cena = "Fase1"; Check(!dados.Valido(), "Não aceita outra cena"); dados.cena = ProgressoJogo.CenaJogo;
        dados.temDiario = true; Check(!dados.Valido(), "Diário exige vitória e baú aberto");
        dados.chefeVencido = dados.bauDiarioAberto = true;
        dados.alavancas = new[] { "AlavancaTriangulo|Switch" };
        dados.baus = new[] { "InteracaoBau|Bau/Chest_Animated" };
        dados.paginaDiario = 6;
        Check(dados.Valido(), "Checkpoint pós-chefe conserva diário, página, alavancas e baús");
        dados.diarioLido = true;
        Check(dados.Valido(), "Checkpoint aceita floresta liberada após leitura do diário");
        dados.diarioLido = false;
        dados.chefeVencido = dados.temDiario = dados.bauDiarioAberto = false;
        Check(!dados.Valido(), "Checkpoint de luta parcial é recusado");
        Check(!new DadosJogo().Valido(), "Objeto vazio não é um save válido");
        Console.WriteLine(aprovados + " verificações aprovadas. Arquivos isolados em " + pasta);
        return 0;
    }
}
