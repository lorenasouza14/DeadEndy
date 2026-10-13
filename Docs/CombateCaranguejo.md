# Combate do caranguejo e as quatro travas

O objetivo é atrair o chefe até uma pedra, esquivar depois que a direção do giro
travar e aproveitar o atordoamento para abrir uma trava do baú. São necessários
quatro atordoamentos aproveitados, com um toque em E por oportunidade.

## Ciclo implementado

1. Ao entrar a 25 unidades do baú, o caranguejo aparece e começa a perseguir Endy.
2. Depois de pelo menos 2 segundos perseguindo, pode dar uma investida curta se
   Endy estiver a até 9 unidades. Essa investida não atordoa o chefe na pedra.
3. Após aproximadamente 8 segundos de combate ativo, o próximo ataque é o giro,
   mesmo que Endy tenha fugido do alcance das investidas comuns.
4. O chefe gira parado durante a preparação de 1,2 segundo, acompanhando Endy.
   Depois trava a direção por 0,35 segundo: essa é a oportunidade de esquivar.
5. Ele avança em linha reta por até 1,5 segundo. Bater numa pedra durante esse
   avanço causa 4 segundos de atordoamento. Errar ou apenas descansar não libera
   o baú. Ficar encostado numa pedra não gera novos atordoamentos.
6. Perto do baú, E abre uma única trava durante esse atordoamento. O progresso
   permanece entre ciclos. Uma oportunidade perdida não fica guardada.
7. A quarta trava abre o baú e encerra os ataques do chefe.

## Ajustes no Inspector

| Componente / campo | Valor inicial | Efeito |
| --- | --- | --- |
| CaranguejoChefe / velocidadePerseguicao | 16 | Endy corre a 15 na cena Demo_Day |
| CaranguejoChefe / velocidadeInvestida | 22 | Velocidade das investidas e do giro |
| CaranguejoChefe / tempoEntreInvestidas | 2 s | Tempo mínimo perseguindo antes da investida comum |
| CaranguejoChefe / tempoAteGiro | 8 s | Ritmo dos ataques que podem atordoar |
| CaranguejoChefe / janelaEsquiva | 0,35 s | Pausa com a direção já fixada |
| CaranguejoChefe / tempoAtordoado | 4 s | Tempo para chegar ao baú e apertar E |
| InteracaoBau / raioInicioCombate | 25 | Distância horizontal até o baú para começar |

Na cena Demo_Day, as velocidades foram atualizadas no componente já salvo.
O trigger do baú passou de 12 × 3 × 7 para 3 × 3 × 3 unidades locais, para exigir
aproximação. O início antecipado do combate independe desse trigger menor.

As referências existentes de chefe, alvo e animadores foram preservadas. São
usados os mesmos gatilhos: Perseguir, Investir, Girar, Atordoar, Interagir e Abrir.
As pedras precisam de collider sólido e tag Pedra no objeto ou em um ancestral.
O Rigidbody do chefe deve continuar dinâmico. O movimento usa
[Rigidbody.linearVelocity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody-linearVelocity.html).

## Verificação

Os scripts de jogo e os testes compilaram sem erros com o compilador e as
referências do Unity 6000.5.7f1 instalado. Os 11 casos de teste estão em
Assets/Tests/Editor/CombateCaranguejoTests.cs. A execução automática ficou
pendente: o editor encerrou com código 1 antes de iniciar o Test Runner.

No Unity, abra Window > General > Test Runner e execute os testes EditMode de
CombateCaranguejoTests. Eles cobrem as quatro oportunidades, spam de E, sair e
voltar ao baú, oportunidade perdida, mira travada, giro à distância, colisores
filhos, pausa e conclusão. Esses testes verificam a lógica; não substituem o
teste de colisões, animações e distâncias na cena em Play Mode.

Para testar a sensação: deixe uma pedra entre a trajetória do chefe e o baú,
posicione Endy entre o chefe e a pedra, espere a direção travar e esquive de lado.
Confira se dá tempo de alcançar o trigger menor antes de terminar o atordoamento.
Ajuste a posição da pedra ou tempoAtordoado conforme esse percurso.

O progresso das travas aparece no Console; ainda não há um indicador de travas
na tela. VidaEndy, na versão atual, só exibe um flash vermelho: não possui redução
de vida nem derrota. A dificuldade deste ajuste vem da execução dos quatro ciclos.
