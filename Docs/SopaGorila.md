# Final da fase: tres bananas e sopa do gorila

## O que foi implementado

Endy pega uma banana com **E**, leva o proprio objeto na mao e aperta **E** perto do caldeirao para depositar. So cabe uma banana na mao por vez, compartilhando o encaixe com as pocoes e outros objetos existentes. Nao foi criada tela de inventario nem estoque de bananas.

O aviso sobre o caldeirao mostra **0/3, 1/3, 2/3 e Sopa pronta! 3/3**. Sao aceitas apenas as tres bananas associadas a receita, cada uma uma unica vez. A terceira entrega torna o gorila docil, cancela seu ataque/sono, inicia o percurso ate o caldeirao e toca **Walk**, depois **Eat**. Ao chegar, ele desativa os bloqueios extras configurados da entrada. Ele permanece comendo e nao volta a atacar nesta partida.

O sistema reutiliza `ItemColetavel`, `DicaInteracao` e o encaixe de mao do `InventarioEndy`. O nome do componente antigo continua o mesmo; nao e necessario criar inventario ou adicionar outro componente no jogador.

## 1. Preparar as tres bananas

Faca isto fora do Play Mode, nas instancias da **Hierarchy**, nao nos prefabs do Project.

1. Selecione apenas o objeto da banana do desafio das armadilhas (a malha da fruta, nao o altar inteiro).
2. Execute **Tools > Gorila > Sopa > Preparar banana selecionada**.
3. Repita com a banana que cai do monolito e a banana do altar ao lado do gorila.

O comando adiciona `BananaSopa` na fruta e cria um filho `InteracaoBanana` com **Sphere Collider / Is Trigger**, `ItemColetavel` e `DicaInteracao`. Tambem acrescenta um Rigidbody cinematico se estiver faltando e desmarca Static. O Rigidbody e os colliders existentes da banana do monolito sao preservados: ela continua caindo normalmente.

Nao marque Pocao, Diario, Pirulito, Trompete ou Radio no Item Coletavel desses sensores. Nao adicione um segundo Item Coletavel na raiz da fruta.

No **Banana Sopa** de cada fruta:

| Banana | Gorila Que Protege | Monolito |
| --- | --- | --- |
| Armadilhas | Vazio | Vazio |
| Ruinas | Vazio | Arraste o objeto com Monolito Desmoronavel |
| Santuario da caverna | Arraste GorillaA com Gorila Guardiao | Vazio |

- A banana protegida so permite coleta enquanto o gorila esta **Dormindo**, nao enquanto esta adormecendo ou acordando.
- A banana das ruinas so permite coleta depois de o monolito terminar de desmoronar e de a fruta estar praticamente parada no chao.
- **Alcance Coleta = 2** metros e **Tamanho Na Mao = 0,35** metro sao valores iniciais. Posicao Na Mao e Rotacao Na Mao permitem acertar a orientacao visual em Play Mode. Copie os ajustes e aplique fora do Play Mode para salvar.
- Se mudar Alcance Coleta, ajuste tambem Radius do Sphere Collider do filho InteracaoBanana. O comando ja compensa a escala ampliada do prefab para deixar o sensor em metros do mundo.
- A banana do monolito deve continuar sendo filha do grupo `monolito`, fora do objeto de pedra que desaparece. Mantenha a referencia **Banana** em Monolito Desmoronavel apontando para o Rigidbody original.

## 2. Conferir o Endy

No objeto que ja possui **Inventario Endy**, mantenha **Encaixe Mao** preenchido com o encaixe existente. O script tambem procura `handslot.r` automaticamente, como antes. Nao remova o componente antigo: ele agora tambem controla o objeto unico que Endy carrega.

Nao adicione `InventarioEndy.Banana.cs` como componente: ele e uma extensao do componente existente. O mesmo vale para os arquivos parciais de sono e caminhada lenta.

Se a mao estiver ocupada com pocao, radio, trompete ou pirulito, e preciso usar/depositar o objeto primeiro. Bananas nao usam R ou F: a entrega e feita com E no caldeirao. Se Endy receber arremesso ou morrer antes da entrega, a banana volta para onde foi pega, sem perder progresso das bananas ja depositadas.

## 3. Preparar a caminhada do gorila

1. Selecione **GorillaA** e adicione **Gorila Sopa**. Mantenha **Gorila Guardiao** e o Animator existentes.
2. Fora da hierarquia do gorila, crie um Empty `PercursoGorilaSopa`, com filhos `SaidaCaverna`, `Contorno` (se necessario) e `PontoComer`.
3. Posicione cada ponto acompanhando o solo. Eles indicam a **posicao do pivo/raiz do gorila**, nao a altura da cabeca. Use como referencia a altura atual da raiz do GorillaA em relacao ao chao.
4. Coloque `PontoComer` ao lado do caldeirao, com espaco para o corpo e os bracos do gorila. Ele deve ficar completamente fora da entrada e nao pode ficar dentro do caldeirao.
5. Em **Percurso**, ajuste Size e arraste os pontos na ordem. O ultimo deve ser PontoComer. Com o gorila selecionado, Gizmos mostra o caminho em rosa.
6. Crie um Empty `OlharSopa` no centro do caldeirao e arraste para **Olhar Para Sopa**.
7. Mantenha **Estado Andar = Base Layer.Walk**, **Valor Andar = 1**, **Estado Comer = Base Layer.Eat**, **Valor Comer = 4**. Esses estados ja existem no Gorilla.controller usado pelo projeto.
8. Comece com **Velocidade = 1,5** e **Velocidade Giro = 180**. Mantenha Apply Root Motion desligado. Nao use um Nav Mesh Agent ou script de demonstracao movendo o mesmo gorila.
9. Em **Bloqueios Da Caverna**, associe somente os colliders extras que fecham a passagem (por exemplo, a barreira invisivel ou o fundo que impede entrar). Eles serao desligados quando o gorila chegar para comer. Nao inclua o piso, o terreno ou as paredes laterais. Se nao existe barreira extra, deixe a lista vazia: o proprio gorila sai do caminho.

**O trajeto e dirigido pelos pontos, sem busca automatica de caminho.** Deixe todos os segmentos livres de pedras, paredes, altar e caldeirao, considerando a largura do gorila. Acrescente pontos nas curvas e nas mudancas de altura para ele nao atravessar objetos ou flutuar. A posicao final precisa realmente deixar um corredor transitavel para Endy.

## 4. Configurar o caldeirao

1. No objeto raiz do caldeirao, adicione **Caldeirao Sopa** e **Dica Interacao**.
2. Em **Bananas Da Receita**, deixe Size = **3** e arraste as tres frutas preparadas, uma em cada campo. Nao repita referencias.
3. Em **Gorila**, arraste GorillaA, que agora possui Gorila Sopa.
4. Em **Jogador**, arraste o Endy que possui Inventario Endy. Existe busca automatica, mas a referencia explicita evita ambiguidades.
5. Crie um Empty filho `PontoDeposito` junto da borda do caldeirao, do lado acessivel ao jogador e na altura do torso. Arraste para **Ponto Interacao**. Esse ponto evita que o centro do modelo ou a escala do asset prejudiquem o alcance.
6. Mantenha **Alcance Deposito = 2,5**. Os colliders solidos do caldeirao devem impedir atravessa-lo. Nao e necessario trigger no caldeirao; ele verifica distancia e obstaculos.
7. Opcional: associe um Particle System em **Vapor Sopa**, com Loop ligado e Play On Awake desligado, para representar o cheiro depois da terceira entrega. **Visual Sopa Pronta** pode apontar para um efeito ou visual filho separado. Nao associe a raiz do caldeirao nem o objeto que contem Caldeirao Sopa nesse campo. A agua ja colocada no caldeirao pode permanecer visivel desde o inicio.
8. Salve a cena.

## 5. Testar a fase

- Pegue a banana das armadilhas com E: ela aparece na mao. Outra banana nao pode ser pega simultaneamente.
- No caldeirao, aperte E novamente: a mao fica livre e o contador passa para 1/3.
- Resolva as ruinas, espere a banana cair e parar, leve-a ao caldeirao: 2/3.
- Espere o gorila dormir, aproxime-se com Ctrl e pegue a banana do santuario. Se ele acertar Endy no caminho, a banana volta ao local de coleta.
- Entregue a terceira banana: o gorila fica docil, vai ate o ponto final e come. Confirme que Endy consegue atravessar a entrada e que o gorila nao volta a atacar.
- A ordem das tres entregas e livre. O contador, o estado docil e os desafios sao estados da partida: reiniciar a cena reinicia a fase. Nao foi acrescentado salvamento entre partidas.

O menu **Tools > Gorila > Sopa > Validar coleta e sopa** verifica coleta, mao unica, bloqueios dos desafios, devolucao, receita unica, caminhada, animacoes e liberacao de colliders numa cena temporaria. O relatorio fica em `Logs/sopa-gorila-validation.txt`. Ele nao monta nem salva os objetos da fase.
