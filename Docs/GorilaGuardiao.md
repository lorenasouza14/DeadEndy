# Guardião da caverna

O gorila fica no posto, vira para Endy e toca o ataque do asset quando ele entra
no alcance. No momento configurado da animação, verifica novamente distância e
paredes: se acertar, chama `VidaEndy.TomarDano(vidaMaxima / 3f)` uma vez e arremessa Endy.
Depois termina o movimento e espera antes de atacar novamente.

Com **Causar Dano** ativo, cada acerto retira 33,33% da vida maxima, inclusive com maximos que nao sao divisiveis por tres. Tres acertos sem cura entre eles sao fatais. A regeneracao continua funcionando, limitada ao maximo. Outros inimigos que chamam `TomarDano()` continuam retirando um ponto. Depois da sopa, o gorila docil nao causa dano nem arremessa.

Validacao: **Tools > Gorila > Testar dano de um terco na fase** verifica fracoes, cura e tres ataques reais em Play Mode. Relatorio: `Logs/gorila-dano-terco.txt`.

## Configuração no Unity

1. Abra a cena `Demo_day_fase_gorilla`. Na Hierarchy, selecione o objeto raiz da
   instância `GorillaA` ou `GorillaB` posicionada na entrada da caverna.
2. Em **Add Component**, adicione **Gorila Guardiao**. Coloque o componente na
   raiz do prefab, acima de todos os colliders do gorila.
3. Arraste o objeto **GorillaA da Hierarchy** (ou o componente Animator dele)
   para **Anim Gorila**. Não arraste o arquivo FBX `Animal_Gorilla_Attack` do
   Project: ele é um asset, não o gorila que está na fase. Arraste o objeto
   que tem **Vida Endy** para **Alvo Endy**. A busca automática existe, mas é
   melhor preencher as referências explicitamente na cena.
4. No Animator, use este Controller do projeto:
   `Assets/Suriyun/Animations_Cute_Animals/Gorilla/Gorilla.controller`.
   Não confunda com o controller da pasta `Addon-PetZoo/Mecanim`.
   Deixe **Apply Root Motion** desmarcado e **Speed = 1**.
   O script também desativa Root Motion e mantém a animação atualizada fora da tela.
5. Não precisa criar um Trigger `Attack`, transições ou Animation Events.
   O controller original já tem o inteiro **animation**, **6 = Attack** e
   **0 = Idle**. Os campos do script já usam os estados corretos:
   `Base Layer.Animal_Gorilla@Attack` e `Base Layer.Idle`.
6. Se usar um prefab `Agent-Gorilla`, desative **Controller Pet Zoo** e
   **Nav Mesh Agent** para que não disputem o movimento/animação com a guarda.
   Desative também qualquer script de demonstração que altere `animation`.
7. No Endy, mantenha **Vida Endy**, **Third Person Controller** e
   **Character Controller** habilitados. Se Vida Endy estiver num filho,
   o Third Person Controller deve estar num ancestral ou descendente dele.
   Não adicione Rigidbody ao Endy. O suporte ao arremesso já foi integrado ao
   Third Person Controller; não precisa adicionar outro componente de arremesso.
   Em Vida Endy, confira **Script De Movimento = Third Person Controller** e
   **Controle De Colisao = Character Controller** para preservar a morte.
8. Mantenha **Guardando Caverna** marcado. O script não identifica uma caverna
   automaticamente: essa opção informa que o gorila está no posto de guarda.
9. Selecione o gorila e ligue **Gizmos** na Scene. Deixe **Detectar Pelo Collider**
   marcado: **Alcance Ataque** é a margem em unidades de mundo a partir da
   superfície do corpo até o centro da cápsula do Endy. Isso funciona mesmo
   com gorilas ampliados. A caixa vermelha é uma aproximação do limite externo.
   Sem collider sólido, usa **Centro Local** como deslocamento em unidades de
   mundo, sem multiplicar pela escala, e mostra uma esfera.
10. Crie um Empty chamado `PontoExpulsaoGorila` fora da caverna, na área segura
    para onde Endy deve voltar. Mantenha-o fora da hierarquia do gorila e
    arraste para **Ponto De Expulsao**. Ele define a direção, não um teleporte
    nem um destino garantido. Sem ele, Endy é lançado para longe do gorila,
    o que pode empurrá-lo para dentro se conseguir chegar por trás.
11. Para bloquear fisicamente a entrada, ajuste um **Capsule Collider** ou
    **Box Collider** sólido no gorila (**Is Trigger desmarcado**). Se necessário,
    use uma barreira invisível na passagem. O script detecta por distância,
    portanto não exige Sphere Collider de detecção nem Rigidbody no gorila.
    Não deixe a cápsula do Endy começar sobreposta a esses colliders.
12. Em **Obstaculos**, inclua as camadas das paredes e pedras. O padrão Everything
    funciona ignorando os colliders do próprio gorila e do jogador, e triggers.

## Valores iniciais para ajustar

| Campo | Inicial | Efeito |
| --- | --- | --- |
| Alcance Ataque | 3 | Distância para iniciar e acertar |
| Intervalo Entre Ataques | 1,5 s | Descanso após terminar o clip |
| Momento Impacto | 0,4 | Golpe aos 40% do clip |
| Velocidade Arremesso | 12 | Velocidade horizontal inicial |
| Impulso Vertical | 5 | Força inicial para cima |
| Duracao Arremesso | 0,8 s | Tempo sem caminhar/pular/esquivar |
| Desaceleracao | 8 | Redução da velocidade horizontal |

O momento exato de contato depende da escala e posicionamento do seu gorila.
No Play Mode, observe a mão e ajuste **Momento Impacto**: diminua se o dano vier
atrasado; aumente se vier antes do contato. Ele usa a fração do clip, então
acompanha mudanças de velocidade do Animator. O clip toca uma vez por ataque,
mesmo se o arquivo importado estiver com Loop Time ativado.

## Teste na fase

- Aproxime Endy: gorila vira, bate, desconta um terço da vida máxima e lança Endy para fora.
- Segure movimento ou tente AA/DD durante o impacto: não deve cancelar o arremesso.
- Saia da esfera antes do contato: gorila erra, sem dano nem arremesso.
- Fique perto por vários ataques: apenas uma aplicação de dano (1/3 da vida máxima) por animação.
- Teste perto de paredes: o golpe não atravessa obstáculos e a cápsula limita o arremesso.
- Pause durante o arremesso: deslocamento e ataque devem congelar.
- Desmarque Guardando Caverna durante a preparação: não deve aplicar o golpe pendente.
- Teste com um terço da vida ou menos: prevalece a morte existente, sem reativar movimento/cápsula
  nem arremessar o corpo. Vida Endy regenera por padrão; desmarque Regenerar Vida
  temporariamente se quiser observar a contagem sem recuperação.

## Ligação futura com a sopa

`gorila.DefinirGuarda(false)` cancela a guarda e qualquer golpe pendente.
`gorila.DefinirGuarda(true)` reativa a guarda após o intervalo de recuperação.
Esses métodos também podem ser chamados por UnityEvent com argumento bool.
A receita, o caldeirão, a animação de comer, a saída do gorila e a abertura de
uma eventual barreira ainda precisam ser ligados ao puzzle quando ele for criado.
Desligar a guarda não remove os colliders sólidos nem abre a passagem sozinho.

## Verificação isolada

Fora do Play Mode, use **Tools > Gorila > Validar ataque e arremesso**.
`GorilaGuardiaoValidation.Run` também pode ser executado com o Unity em batchmode usando
`-executeMethod GorilaGuardiaoValidation.Run`. Ele cria uma cena temporária,
testa o controller original, dano único, alcance, obstáculos, direção de expulsão,
colisão, pausa e cancelamento, e grava `Logs/gorila-validation.txt` sem salvar a fase.
O alinhamento visual da mão com Endy deve ser conferido na cena real.
