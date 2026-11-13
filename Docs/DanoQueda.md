# Dano de queda

Integrado ao ThirdPersonController existente, sem componente adicional.

- Altura segura: **12 unidades** do mundo.
- Dano: **max(0, altura da queda - 12) × 0,25**, a 0,25 ponto por unidade excedente.
- Exemplos: 12 unidades = 0; 16 = 1; 20 = 2; 32 = 5 pontos.
- Ajuste de balanceamento para reduzir a penalidade das catapultas. Estes novos valores ficaram para teste manual do jogador.
- A altura e medida do ponto mais alto do salto/queda ate o contato real da capsula com o chao. Encostar de lado numa parede nao encerra a queda.
- Dano aplicado uma vez por pouso, inclusive apos arremesso do gorila ou catapulta das armadilhas. O dano de impacto do gorila e separado do eventual dano ao cair de uma altura perigosa.
- Pulos normais e pequenos degraus nao ultrapassam o limite. Pause congela a medicao. Dano, regeneracao e morte usam VidaEndy.
- Os campos **Receber Dano De Queda**, **Altura Segura Queda** e **Dano Por Unidade Queda** ficam no ThirdPersonController do Endy.
- Teleportes externos reiniciam a medicao. Para respawn customizado, chame `ReiniciarMedicaoQueda()` depois de posicionar o jogador. Desativar o movimento tambem limpa a medicao.

**Tools > Player > Validar dano de queda** testa o Endy real em Play Mode sobre um piso temporario: limites, dano progressivo, pause, salto normal, arremesso, catapulta, teleporte, repeticao de pouso e queda fatal. O piso e os deslocamentos de teste nao sao salvos na fase. Relatorio: `Logs/dano-queda-validation.txt`.
