# Teste de bancada

## Preparacao

1. Grave no equipamento o HEX correspondente à versão que será testada.
2. Conecte o LCD, a alimentação e o cabo USB ao computador.
3. Execute `dist\GerenciadorColheita.exe` e selecione `1.5.3` ou `1.6`.

## Download

1. No equipamento, selecione `Descarregar Dados`.
2. Confirme e aguarde a mensagem `USB conectado!`.
3. No programa do PC, clique em `Baixar dados`.
4. Confirme que o progresso chega de 1 a 32 pacotes.
5. Confirme que o equipamento mostra `Dados enviados`.
6. Confirme que o programa informa checksum válido.

O download normal deve levar aproximadamente 1,6 segundo, pois o firmware mantém
50 ms entre os 32 pacotes.

## Conteudo

1. Compare as glebas exibidas no programa com o histórico do LCD.
2. Salve a EEPROM em `.bin` e confirme que o arquivo possui 256 bytes.
3. Exporte o CSV e confira data, variedade, forças, diagnóstico, freio,
   vibração e velocidade.
4. Gere o PDF e confira todas as glebas cadastradas.

## Compatibilidade

1. Execute o programa antigo e solicite um download.
2. Confirme que ele ainda recebe 32 pacotes.
3. O firmware envia `0xFF` no byte 252 para qualquer comando diferente de `2`.
4. O programa novo usa o comando `2` e recebe o checksum real no byte 252.

## Falhas

- Desconecte o cabo durante o envio: o equipamento deve mostrar falha após o timeout.
- Feche o programa antes do fim: o Watchdog não deve reiniciar o equipamento.
- Envie o comando zero: o firmware deve aguardar 1 segundo e desconectar.
- EEPROM com checksum inválido: o programa deve baixar os dados, mostrar o aviso e
  ainda permitir salvar a imagem bruta para diagnóstico.
