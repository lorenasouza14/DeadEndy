# Menu de pause

- **Esc** ou **Start** do controle abre e fecha o menu.
- **Retornar** continua do ponto em que o jogo parou.
- **Sair** fecha o jogo; na Unity, encerra o Play Mode.
- Mouse, teclado e controle usam os mesmos botões. Hover/seleção aumentam o texto em 5% e aquecem sua cor; pressionar reduz a escala para 97%.

`VidaEndy` instala automaticamente `Assets/Resources/MenuPause.prefab` ao iniciar. O prefab referencia `Assets/Videos/menu_pause.mp4` e a fonte Doctor Soos Bold 2. A interface é criada durante a execução; não é necessário configurar cada cena. Quando existe um texto no menu de morte, sua fonte, material, estilo, cor e tamanho são reaproveitados. O padrão é tamanho 70, branco, sem redimensionamento automático.

O vídeo mantém sua proporção, toca em loop e usa tempo independente da pausa. Os botões ficam na região inferior. O canvas adapta a composição à resolução, tomando 1920 × 1080 como referência. A trilha do vídeo, caso exista, continua enquanto o áudio do jogo pausa.

O sistema restaura a velocidade do tempo, o áudio, o cursor e os bloqueios anteriores ao retornar. Controles de movimento e câmera ficam bloqueados sem cancelar esquivas ou arremessos. Puzzles e recuperação de vida ficam congelados. O menu não abre durante o game over e libera a pausa se o jogador morrer, for desativado ou sair da cena.

## Validação

`PauseValidation.Run` é uma rotina para uma instância separada da Unity em `-batchmode -executeMethod PauseValidation.Run`. Ela usa uma cena de teste vazia, verifica os recursos, Esc, vídeo, animações, retorno, dano, preservação dos controles, morte e saída. O resultado é escrito em `Logs/pause-validation.txt`, com uma captura em `Logs/pause-preview.png` quando a reprodução funciona.

Nesta implementação, a compilação dos scripts de jogo, controles e editor passou. A execução da validação ficou pendente porque a Unity encerrou durante sua inicialização, com falha de validação do cliente de licença. A aparência e a reprodução precisam ser conferidas em Play Mode quando o editor estiver disponível.
