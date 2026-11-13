# Cena unificada das fases 1 e 2

Abra `Assets/Cenario/PNB_Tropical_Jungle/Scenes/juncao_fase1-2.unity`.
O menu principal inicia essa cena. As duas cenas de origem foram preservadas.

A junção usa `Demo_Day` como base e incorpora as alterações de
`Demo_day_fase_gorilla` em relação ao ancestral comum `47fe1636`:

- Caverna, gorila, caldeirão, percurso da sopa e três bananas.
- Rampa com 22 armadilhas, destinos de lançamento e banana do altar.
- Desafio dos três altares e monólito.
- Alterações e remoções de rochas, vegetação e objetos de apoio da fase do gorila.
- Alavancas, tochas, cinco baús, chefe caranguejo, diário, interface e bloqueios atualizados da primeira fase.

Objetos compartilhados mantêm seus identificadores: há um único Endy, uma câmera
principal, um EventSystem e um terreno. As referências da receita, das armadilhas
e da mão do personagem apontam para objetos da cena unificada.

O novo `Assets/Terrain/Demo_Day_Juncao.asset` combina as 835 amostras de altura
alteradas na primeira fase e as 588 da caverna, sem conflitos entre elas. As
camadas e a pintura são idênticas nas origens. Os terrenos originais não foram
substituídos.

O reconhecimento da cena pelos limites da primeira fase e pelo autosave também
aceita a junção. Saves antigos da `Demo_Day` continuam válidos. O sistema de save
existente continua registrando o progresso da primeira fase; esta junção de
cenário não acrescenta persistência dos puzzles de bananas, altares ou sopa.

## Verificação e reprodução

- `Tests/RunAutosaveChecks.ps1`: compilação dos scripts e verificações de persistência.
- `Tools > Mapa > Juncao > Validar cena unificada`: referências, objetos essenciais
  e imagens de inspeção em `Logs/juncao`. Requer cenas abertas sem alterações não salvas.
- `node Tests/UnirCenas.mjs`: refaz a combinação dos objetos a partir das duas
  cenas originais e do ancestral no Git LFS. **Substitui a cena de junção**, guardando
  uma cópia anterior em `Logs/juncao/antes-da-juncao.unity`; use antes de fazer
  edições manuais adicionais na cena unificada.
- Após refazer os objetos, `Tools > Mapa > Juncao > Combinar terrenos das duas fases`
  recompõe o relevo e executa a validação. O script de reprodução extrai um terreno
  ancestral temporário para essa etapa.

Relatórios da junção e da verificação em execução ficam em `Logs/juncao`.
