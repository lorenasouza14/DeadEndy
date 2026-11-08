# Gorila: sono e aproximacao silenciosa

A instancia do gorila em `Demo_day_fase_gorilla` ja esta salva com **Usar Ciclo Sono marcado** e **Causar Dano desmarcado**. Os passos abaixo servem para conferir ou ajustar. O primeiro sono ocorre depois de 20 a 35 segundos de jogo; observe parado ou fora do raio de audicao para nao acorda-lo durante a aproximacao.

O comportamento foi integrado ao `GorilaGuardiao` existente. A caminhada lenta foi integrada ao `ThirdPersonController` do Endy. Os arquivos `.Sono.cs` e `.Furtividade.cs` completam esses componentes: **nao tente adiciona-los separadamente no Inspector**.

## Configuracao na cena

1. Fora do Play Mode, abra `Demo_day_fase_gorilla`. Expanda o grupo da entrada da caverna e selecione a instancia do gorila que ja tem **Gorila Guardiao**. O componente fica no gorila, nao no altar, na banana ou no santuario inteiro.
2. No **Gorila Guardiao**, mantenha **Guardando Caverna** marcado, marque **Usar Ciclo Sono** e **desmarque Causar Dano**. Essa ultima opcao faz o golpe arremessar sem tirar vida; os demais inimigos continuam com seu dano normal.
3. Confirme **Anim Gorila** apontando para o Animator da instancia da Hierarchy e **Alvo Endy** apontando para o objeto com Vida Endy. Mantenha o controller `Assets/Suriyun/Animations_Cute_Animals/Gorilla/Gorilla.controller`, sem scripts de demonstracao disputando a animacao.
4. Em **Desafio furtivo - sono**, use os valores abaixo. `X` e o tempo minimo e `Y` o maximo. O script sorteia um novo tempo a cada ciclo.

| Campo | Valor inicial |
| --- | --- |
| Intervalo Acordado | X = 20, Y = 35 segundos |
| Intervalo Dormindo | X = 8, Y = 12 segundos |
| Tempo Adormecer | 0,8 segundo |
| Tempo Acordar | 1,5 segundo |
| Estado Dormindo | `Base Layer.Rest` |
| Valor Dormindo | **5** |
| Raio Audicao | 7 metros |
| Velocidade Que Acorda | 1,6 m/s |
| Tolerancia Ruido | 0,1 segundo |

5. **Centro Audicao** pode ficar vazio: usa o centro de deteccao do gorila. Se precisar ajustar, crie um Empty na altura do peito, como filho do gorila, e arraste para esse campo. Com o gorila selecionado e Gizmos ligados, a esfera amarela mostra a audicao. Ela e medida em metros do mundo, independentemente da escala do prefab.
6. No objeto do jogador que possui **Third Person Controller**, confira a nova secao **Caminhada lenta - segure Ctrl**. Deixe **Permitir Caminhada Lenta** marcado e **Velocidade Caminhada Lenta = 1**. Segure **Ctrl + WASD** para caminhar devagar; no controle, segure **RB/R1**. Shift continua sendo a corrida normal. O limite lento continua valendo junto de Shift e do bonus de velocidade da pocao.
7. Posicione o altar de modo que o jogador consiga chegar ate a banana quando o gorila dorme. O caminho desde a borda segura deve caber no menor tempo de sono: com 1 m/s e 8 segundos, deixe margem para aproximacao e recuo. A posicao de pegar a banana precisa ficar dentro de **Alcance Ataque** (inicialmente 3), medido a partir do corpo se **Detectar Pelo Collider** estiver marcado. A caixa vermelha e uma aproximacao do alcance.
8. Mantenha **Ponto De Expulsao** apontando para um Empty numa area segura fora da caverna. Ele define a direcao do arremesso, nao um teleporte para uma posicao exata. Ajuste velocidade/impulso observando a aterrissagem, evitando agua, paredes e quedas grandes.
9. Preserve os colliders solidos. **Sono nao abre a entrada.** Se o corpo do gorila nao cobre toda a passagem, crie um Empty `BloqueioEntradaCaverna` diretamente no grupo da entrada, com **Box Collider**, **Is Trigger desmarcado**. Cubra apenas o acesso ao interior, deixando o altar e sua aproximacao do lado de fora. Nao coloque essa barreira como filha do gorila: ela nao deve ampliar a deteccao pelo corpo. Essa barreira sera desativada futuramente pela sopa.
10. Salve a cena com Ctrl+S.

## O que acontece no jogo

- Comeca acordado. Ao terminar o intervalo, entra em Rest e permanece em repouso.
- Nao ha contagem, mensagem, bocejo de aviso nem sinal antecipado de despertar.
- Ao terminar o sono ou ouvir Endy, faz uma transicao gradual de Rest para Idle. Somente depois de **Tempo Acordar** pode iniciar Attack. O contato continua respeitando **Momento Impacto** da animacao original.
- Dentro da esfera amarela, velocidade horizontal real acima do limite acorda o gorila. Pular ou esquivar tambem acorda. Ficar parado ou andar devagar nao acorda. Fora da esfera, movimento nao interfere.
- Apertar Ctrl no ultimo instante nao interrompe instantaneamente a inercia: reduza a velocidade antes de entrar na area.
- O gorila nao persegue nem aplica pancada a distancia. Recuar para fora do alcance antes do contato evita o arremesso. Ele nao inicia outro ataque enquanto Endy ainda esta sendo arremessado.
- Com **Causar Dano desmarcado**, esse golpe nao retira vida nem dispara game over. Outros perigos do mapa continuam funcionando.
- A coleta da banana e o inventario continuam para outra etapa. `EstaDormindo` fica disponivel para a futura interacao da banana; `DefinirGuarda(false)` continua disponivel para a futura sopa, sem remover barreiras automaticamente.

## Conferencia rapida

Para testar sem esperar tanto, coloque temporariamente Intervalo Acordado em (3, 3). No Play Mode: espere dormir, aproxime-se com Ctrl, solte Ctrl e ande, observe a transicao e recue. Repita ficando perto para receber o golpe, conferindo que a vida nao cai. Corra longe do gorila e confira que ele permanece dormindo. Restaure (20, 35) para o ritmo final.

O menu **Tools > Gorila > Validar sono e furtividade** executa verificacoes isoladas numa cena temporaria e grava `Logs/gorila-sono-validation.txt`. Ele nao configura nem salva a fase. O menu anterior **Validar ataque e arremesso** continua cobrindo o ataque com dano habilitado.
