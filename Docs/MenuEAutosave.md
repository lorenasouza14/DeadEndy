# Menu e salvamento automático

Abra `Assets/MenuPrincipal.unity` e entre em Play Mode. O menu é a primeira
cena habilitada no Build Settings; `Demo_Day` vem em seguida.

- **Novo Jogo:** inicia `Demo_Day`. Se houver um arquivo anterior, pede confirmação.
  O arquivo anterior só é substituído quando o primeiro checkpoint novo é gravado.
- **Continuar:** carrega o último checkpoint válido. Fica apagado e indisponível
  quando não existe save utilizável.
- **Créditos:** abre um painel. Edite o campo `Creditos` no `MenuManager`, em
  `GerenciadorMenu`, para colocar os nomes da equipe. O texto inicial é provisório.
- **Sair:** fecha o jogo exportado; no editor, encerra o Play Mode.

O script foi renomeado de `MenuManeger.cs` para `MenuManager.cs`, preservando seu
GUID e as referências dos botões. As áreas clicáveis foram alinhadas com os textos
nas posições originais. O EventSystem é criado pelo menu se ainda não existir.
Os textos dos botões aumentam suavemente e ganham destaque dourado ao passar o
mouse/selecionar pelo teclado, e encolhem brevemente ao pressionar. A área de
clique permanece fixa. Botões indisponíveis ficam apagados e não animam.

Se MenuPrincipal e Demo_Day estiverem abertos juntos no editor, o menu desativa
os objetos da fase somente durante o Play Mode e assume o cursor. Ao escolher
Novo Jogo ou Continuar, a fase é carregada sozinha. Isso evita movimento, captura
do mouse e salvamento em segundo plano enquanto o menu está na tela.

## O que fica salvo

Um único slot guarda posição e direção seguras, vida naquele ponto, alavancas
concluídas e suas tochas/bilhete, vitória e posição do chefe encolhido, baús de
combate já abertos após a vitória, baú roxo, diário adquirido e última dupla lida.

Na fase do gorila, também ficam salvos os giros de cada altar das ruínas, o
puzzle resolvido/monolito derrubado, a banana na mão, cada banana entregue ao
caldeirão e a passagem liberada. Coletar ou entregar uma banana, terminar um
giro, resolver as ruínas e liberar a passagem solicitam um checkpoint imediato.
Ao continuar com 1/3 ou 2/3, as frutas entregues permanecem removidas. Com 3/3,
a sopa permanece pronta; se o gorila ainda estava andando, ele retoma o trajeto.
Se já havia chegado, reaparece comendo no destino e a passagem continua aberta.
Saves antigos continuam válidos, com a fase do gorila no estado inicial porque
esses arquivos ainda não registravam suas conquistas.

O autosave periódico grava a cada **5 minutos (300 segundos) de jogo ativo**.
Menu, pausa e tempo sem foco não contam no relógio. Não há gravação automática
imediata só por entrar na cena ou clicar na janela. O jogo também salva nas
conquistas acima, ao fechar o diário, ao pausar a aplicação e ao sair da partida.
Esses checkpoints de progresso e saída continuam independentes do intervalo.
A mensagem **Progresso salvo** só
aparece depois de concluir a escrita. O intervalo pode ser editado no componente
`AutosaveEndy` do Endy, com mínimo de 300 segundos. Nenhum desses gatilhos grava
quando o menu está aberto ou a cena do Endy não é a cena de jogo ativa.

Uma posição só vira ponto seguro após meio segundo no chão, sem combate,
ação de item, poção ativa, item temporário na mão, areia movediça ou dano recente.
A banana pode acompanhar o ponto seguro, pois agora também é restaurada. A margem
da entrada da arena também é excluída. Conquistas podem ser gravadas junto da
última posição segura, mesmo que Endy esteja no ar naquele instante.

## Regra da luta e do Game Over

Antes de fechar a arena/iniciar o puzzle do baú, o jogo conserva o checkpoint
externo. Durante a luta não grava posições, baús parciais, itens consumíveis ou
efeitos temporários. Sair ou morrer durante a luta retorna ao checkpoint anterior,
com a luta e seus itens reiniciados. Após vencer, o chefe permanece pequeno e a
arena fica liberada ao continuar. Não há replay da animação de encolher.

**Reiniciar** no Game Over agora carrega o checkpoint válido. O fade de despertar
continua funcionando no carregamento. Testar `Demo_Day` diretamente no editor
também recupera seu save de teste, se existir; use **Novo Jogo** no menu para zerar.

## Arquivos e recuperação

Os arquivos ficam em `Application.persistentDataPath`. Com os nomes atuais do
projeto no Windows, a pasta é `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project`.

- Jogo exportado: `autosave.json`.
- Play Mode no editor: `autosave-editor.json`, separado da partida exportada.
- `.bak`: checkpoint anterior validado; `.tmp`: escrita ainda em andamento.

A gravação usa arquivo temporário, flush e substituição atômica. O conteúdo é
versionado e tem verificação SHA-256 para detectar corrupção. Se o principal
falhar, o backup válido pode ser carregado. Se ambos forem inválidos, Continuar
é desabilitado e o problema é exibido. Iniciar a cena diretamente nessa condição
não sobrescreve os arquivos: escolha Novo Jogo conscientemente pelo menu.

Alavancas e baús possuem `idSave` serializado. Preserve esses IDs ao editar o
cenário para manter compatibilidade com partidas existentes. Cenas ainda abertas
antes da instalação também funcionam: o código adiciona o autosave ao personagem
e usa IDs derivados dos caminhos originais dos objetos quando o campo está vazio.

## Verificação

Execute `Tests/RunAutosaveChecks.ps1` no PowerShell para compilar os scripts com
as referências locais do Unity e executar 44 verificações das classes reais de
persistência. Elas usam arquivos isolados em `Logs/autosave-tests`, sem tocar no
progresso do jogador. O relatório fica em `Logs/autosave-persistence-checks.txt`.

Esses testes cobrem validação de dados, backups, corrupção, interrupção,
arquivo bloqueado e falhas de escrita. Não substituem a conferência em Play Mode:

1. Começar pelo menu sem save; conferir Continuar indisponível.
2. Iniciar jogo, andar e aguardar cinco minutos de jogo ativo e Progresso salvo;
   sair do Play Mode.
3. Abrir o menu novamente e continuar na posição segura.
4. Acionar alavancas; recarregar e conferir tochas e bilhete.
5. Entrar na luta, abrir baús e morrer; conferir retorno ao checkpoint externo.
6. Vencer e coletar o diário; recarregar e conferir chefe pequeno, arena livre,
   baús abertos, diário e última página.
7. Cancelar Novo Jogo; conferir que Continuar mantém a partida anterior.
8. Abrir MenuPrincipal e Demo_Day juntos, dar Play e verificar mouse visível,
   cliques, fase suspensa e ausência de autosave no menu. Conferir hover/clique,
   inclusive nos botões dos diálogos.
9. Na cena integrada, sair e continuar após cada giro das ruínas, após resolver
   o monolito e após pegar cada banana; conferir altares e banana na mão.
10. Repetir com 1/3 e 2/3 entregues, durante a caminhada do gorila e após a
    passagem abrir. Conferir contador, ausência de frutas duplicadas e gorila dócil.

**Tools > Gorila > Sopa > Validar coleta e sopa** também testa a restauração em
objetos novos após serializar os dados, incluindo entregas parciais, banana na
mão, caminhada pendente, passagem aberta e ruínas. Usa uma cena temporária e
gera `Logs/sopa-gorila-validation.txt`, sem alterar o save do jogador.
