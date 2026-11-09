# Limites da primeira fase

O bloqueio do mato usa os seis cubos-guia da cena Demo_Day (Cube e Cube (1) a Cube (5), na raiz), cada um com BloqueioFloresta. Os cubos continuam visíveis no editor para facilitar ajustes; durante o jogo ficam invisíveis e seus colliders impedem a passagem até Endy coletar e abrir o diário com J.

Para mudar uma passagem, mova, gire ou redimensione o próprio cubo. Para criar outra, duplique um cubo já configurado. LimitesPrimeiraFase encontra os componentes BloqueioFloresta da cena, independentemente do nome. O contorno antigo foi substituído pelos guias e por trechos complementares no objeto Vedacao complementar da floresta. Esses trechos unem as pontas dos cubos, acompanham as encostas e fazem um recuo junto ao lago para manter o percurso das alavancas. As junções se sobrepõem para impedir brechas com Endy encolhido. Se mover um guia, use Tools > Mapa > Vedar passagens entre os cubos da floresta para ajustar as junções.

A largura, a espessura e a direção seguem o cubo. Somente a altura da colisão recebe uma margem durante a partida (10 metros acima e abaixo, configurável no componente), evitando passar por baixo ou saltar a parede. As dimensões salvas do cubo não são alteradas.

A área dos cinco baús abre após acender as três tochas. A entrada norte fica aproximadamente 12,6 metros depois da alavanca do norte, antes de contornar o relevo e avistar a arena. O bloqueio lateral continua ativo até completar as tochas. Perto de um bloqueio aparece o objetivo que falta. A primeira abertura do diário registra diarioLido no autosave; saves sem esse campo continuam válidos e pedem abrir novamente o diário.

A correção de checkpoint usa apenas as novas colisões da floresta e a área dos baús. A região inteira do antigo contorno não provoca mais retorno ao início.

Validação: Tools > Mapa > Validar limites da primeira fase. As colisões dos guias e trechos complementares são testadas em cópias isoladas, incluindo Endy normal/encolhido, passagem após leitura e restauração do progresso. Também são verificadas as rotas pelo terreno até as alavancas, o isolamento da mata e a obstrução da vista dos baús na nova entrada. Resultado: Logs/limites-fase-validation.txt. Não altera saves da partida.
