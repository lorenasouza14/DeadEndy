# Puzzle dos altares das ruínas

Cena: `Demo_day_fase_gorilla`, grupo `desafio_alinhar_totens`.

Perto de uma alavanca aparece **[E] Acionar alavanca**. Cada acionamento gira a pedra 90 graus e também gira a próxima: 1 → 2, 2 → 3, 3 → 1. A base e a alavanca permanecem no lugar. O feixe rosa aparece quando a pedra está voltada para o monólito e apaga durante o giro ou ao perder o alinhamento.

Ao alinhar as três pedras, o monólito treme, se parte, solta a banana e provoca tremor na câmera. Os fragmentos desaparecem após o desmoronamento. A banana permanece no chão, sem coleta nem inventário.

## Componentes já associados

- `PuzzleAltaresRuinas` no grupo principal: referências dos três altares, jogador, monólito, alcance de interação e duração do giro.
- `AltarRuinas` e `DicaInteracao` em cada `toten_1`, `toten_2` e `toten_3`: pivô da pedra, alavanca, feixe e orientação correta.
- `MonolitoDesmoronavel` em `monolito`: corpo intacto, nove fragmentos, banana, poeira e impulso da câmera.
- `CinemachineImpulseListener` em `PlayerFollowCamera`, recebendo o canal 8 do monólito.
- `Rigidbody` e `BoxCollider` na banana. Ela começa imóvel e recebe gravidade na queda.

## Ajustes

No grupo principal, altere `Alcance Interacao` (2,3 m) e `Duracao Giro` (0,65 s). Em cada altar, `Passos Iniciais` define de 0 a 3 quartos de volta a partir da orientação alinhada. No monólito, ajuste `Antecipacao`, `Tempo Dos Fragmentos` e `Intensidade Tremor`.

O estado inicial é **0, 1, 3**. Uma solução é acionar o **altar 1 três vezes** e o **altar 3 uma vez**. Se mudar os passos iniciais, mantenha a soma par para existir uma solução com giros de 90 graus ligados em pares.

`Tools > Gorila > Testar puzzle das ruinas` executa uma verificação em Play Mode e registra o resultado em `Logs/ruinas-play-validation.txt`. O teste move o jogador temporariamente, aciona as três alavancas pela entrada E e sai do Play Mode ao terminar. As posições de edição são preservadas.

`Tools > Gorila > Configurar puzzle das ruinas` foi usado para montar os componentes; a cena já está configurada e não precisa executar essa opção novamente. Materiais e malhas dos fragmentos estão em `Assets/fase_gorila/PuzzleRuinas`.
