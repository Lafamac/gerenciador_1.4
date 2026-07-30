# Atualizacoes realizadas na versao 1.5.3

## Correcao USB e Watchdog para PICkit 3

- Aplicada na versao 1.5.3 a inicializacao USB validada na versao 1.6.
- A inicializacao executa `usb_detach()` e mantem 500 ms de desconexao
  visivel ao Windows antes de religar o transceptor.
- A espera pela alimentacao USB usa ciclos de 100 us, timeout de 5 segundos
  e alimentacao continua do Watchdog.
- A enumeracao HID ocorre em uma etapa separada, tambem com ciclos de 100 us,
  timeout de 10 segundos e alimentacao do Watchdog.
- O botao RETORNA permite cancelar a conexao sem prender o equipamento.
- Falha de alimentacao ou enumeracao mostra `USB nao reconhec.` e
  `Verifique o cabo`, desliga o USB e retorna ao equipamento.
- O descritor HID local usa intervalo de 10 ms nos endpoints de entrada e
  saida.
- A pilha usada e a nativa do CCS: `pic18_usb.h`, `usb_desc_hid.h` local e
  `usb.c`.
- O envio permanece em 32 pacotes de 8 bytes, totalizando os 256 bytes da
  EEPROM.
- O comando legado continua enviando `0xFF` no byte 252; o comando novo envia
  o checksum real, preservando compatibilidade com os dois gerenciadores.
- `#build(reset=0x1100, interrupt=0x1108)` e a reserva ate `0x10FF` foram
  desativados para gravacao direta completa com PICkit 3. O HEX desta versao
  usa vetores nativos em `0x0000` e `0x0008`.
- Criado `codigo_v1_5_3_pickit3.ccspjt` com os caminhos de inclusao usados na
  compilacao direta.

Verificacao local da versao 1.5.3:

```text
CCSCON.exe +FH +DF +LN codigo_v1_5_3.c
0 Errors, 0 Warnings
ROM=78%
RAM=11% - 14%
```

## Compatibilidade preservada

- MCU PIC18F2550 e compilador CCS C mantidos.
- Hardware, pinagem e cristal externo de 20 MHz mantidos.
- Registros das 12 glebas continuam usando 21 bytes cada.
- Datas continuam nos enderecos 253, 254 e 255.
- USB HID continua usando endpoint 1, 32 pacotes de 8 bytes.
- O descritor HID local foi ajustado somente no intervalo de polling.

## Robustez

- WDT ativado com `WDT1024`.
- Loops demorados e atrasos longos alimentam o WDT.
- Botoes receberam debounce de 30 ms e espera de soltura.
- Divisoes variaveis agora verificam denominador zero.
- Dados fora das faixas basicas da EEPROM sao normalizados em RAM.

## Balanca e ADC

- ADC configurado com `ADC_CLOCK_DIV_64` para clock de CPU de 48 MHz.
- Pesagens usam media de 32 amostras separadas por 200 us.
- A conversao usa diferenca com sinal e zona morta de 2 bits.
- Leituras abaixo do zero nao geram underflow.
- O zero inicial e filtrado e continua armazenado somente em RAM.
- O modo de calibracao mostra o valor filtrado; CONFIRMA atualiza o zero.

## EEPROM

- Gravacoes repetidas sao evitadas por `eeprom_write_if_changed`.
- A limpeza percorre os enderecos 0 a 255.
- O byte 252 guarda XOR dos bytes 0 a 251.
- Salvar avaliacao e limpar memoria atualizam o checksum.
- Checksum invalido mostra `Memoria verificar`, mas nao bloqueia o uso.

Nota: depois da limpeza, todos os 256 bytes sao percorridos e apagados com
`0xFF`; em seguida o byte 252 recebe o checksum valido do bloco 0..251.

## USB

- Corrigido o bug sem efeito `pacote == 0`.
- Cada nova resposta inicia em `pacote = 0`.
- Removido o salto `goto` do envio.
- Buffers de entrada e saida iniciam zerados.
- A rotina alimenta o WDT junto de `usb_task()`.
- Desconexao chama `usb_detach()` e encerra a rotina.
- Ao concluir, o LCD mostra `Dados enviados`.
