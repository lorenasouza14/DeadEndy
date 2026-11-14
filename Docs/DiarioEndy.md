# Diário do Endy

A cena `Demo_Day` tem um componente `DiarioEndy` no personagem e um
`RecompensaDiario` no objeto `Recompensa do chefe - diário`.
Este último oculta `Chest_Purple` e `PP_Pillar_Stone_08 (3)` no início da partida
e revela os dois quando o chefe termina de encolher (após os dois segundos da animação).
As posições e os modelos existentes foram preservados.

Perto do baú roxo, **E** abre a tampa. Quando ela terminar de abrir, outro
**E** guarda o diário. Ele não ocupa a mão nem é consumido ao ler ou usar outros
itens. A aquisição e a última página são guardadas pelo autosave de pontos
seguros e recuperadas ao continuar a partida (ver `Docs/MenuEAutosave.md`).

## Leitura

- **J** abre o livro depois da coleta.
- **A / D**, **setas**, botões **Anterior / Próxima** ou arraste horizontal
  com o mouse viram uma dupla de páginas, com animação.
- **J**, **Esc** ou **Fechar** fecham o livro.
- A leitura pausa o mundo e bloqueia movimento/câmera. Fechar restaura o estado
  anterior, incluindo velocidade do tempo e cursor.
- O livro reabre na última dupla visitada. Não permite ultrapassar os limites
  nem iniciar outra folheada durante a animação.

## Colocar as imagens prontas

1. Importe as imagens no Unity com **Texture Type = Sprite (2D and UI)** e
   **Sprite Mode = Single**. Use resolução de importação suficiente para ler
   os textos (por exemplo, 2048 ou 4096, conforme o tamanho das imagens).
2. Selecione Endy e encontre o componente **Diario Endy**.
3. No campo **Paginas**, defina a quantidade e arraste **uma imagem por página**
   em ordem: elemento 0 é a página 1 à esquerda, elemento 1 é a página 2 à
   direita, e assim por diante.

As imagens são ajustadas à largura e à altura de cada folha, preenchendo-a
até as bordas, sem recortar o conteúdo. A capa e os botões usam tons de marrom.
Se o último par tiver apenas uma página, o outro
lado fica vazio. Enquanto a lista não recebe imagens, aparecem seis páginas
provisórias para testar o livro. Entradas vazias usam o texto provisório.
Imagens que já contenham uma dupla inteira precisam ser separadas em duas.

## Manutenção e validação

`Tools > Diario > Atualizar imagens das paginas` atribui as 12 imagens
`Assets/Imagens/pagina1.png` a `pagina12.png` aos diários das cenas de jogo
abertas e salva essas cenas fora do Play Mode. Uma cópia anterior fica em
`Logs/diario-paginas-before-*.unity`. Use esse comando se a cena aberta ainda
mantiver a lista antiga depois de uma alteração externa.
As páginas 8–12 apresentam caverna, ruínas, caldeirão, guardião dormindo e morro,
nessa ordem.

`Tools > Diario > Configurar diario na cena` reconecta os componentes na cena
aberta sem substituir as páginas configuradas. Antes de salvar, cria uma cópia
da cena em `Logs/diario-before-*.unity`.

`Tools > Diario > Validar diario em Play Mode` executa o fluxo de recompensa,
abertura, coleta, pausa, folheada, limites, páginas ímpares, interrupção e morte.
O resultado vai para `Logs/diario-validation.txt`, com capturas em
`Logs/diario-aberto.png` e `Logs/diario-folheando.png`.
O teste encerra o Play Mode e suas alterações de jogo não são salvas.
