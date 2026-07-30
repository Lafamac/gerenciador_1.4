# Plano de simulacao e testes da versao 1.5.3

## Gravacao direta e USB corrigido

1. Apagar completamente o PIC18F2550 e gravar `codigo_v1_5_3.hex` pelo
   PICkit 3.
2. Nao usar este HEX por meio do bootloader antigo: ele possui vetores nativos
   em `0x0000` e `0x0008`.
3. Desligar e ligar novamente o equipamento.
4. Entrar em `Descarregar dados` sem o cabo conectado e confirmar que o LCD
   solicita a conexao.
5. Conectar o cabo. Confirmar `Conectando USB` / `Aguarde...` e depois
   `USB conectado!`.
6. No Gerenciador Colheita, solicitar os dados e conferir exatamente 32
   pacotes de 8 bytes.
7. Repetir a descarga sem reiniciar o equipamento.
8. Retirar o cabo durante a espera e durante uma transferencia.
9. Pressionar RETORNA durante a tentativa de conexao.
10. Deixar a conexao falhar e confirmar `USB nao reconhec.` /
    `Verifique o cabo`, sem reinicio causado pelo Watchdog.

Resultado esperado: o Windows reconhece o dispositivo HID, o equipamento nao
trava nas esperas e o gerenciador recebe os 256 bytes completos.

## Preparacao

1. Compilar `codigo_v1_5_3.c` com CCS PCWHD v5.008 usando PCH/PIC18.
   A verificacao local foi feita com PCH v5.007.
2. Gravar o HEX completo diretamente pelo PICkit 3.
3. Configurar PIC18F2550 com cristal externo de 20 MHz.
4. Ligar LCD 16x2, botoes em RA1..RA4, ADC em AN0 e deteccao USB em RA5.

## Inicializacao e WDT

1. Ligar sem peso na balanca e confirmar a tela `Zerando balanca`.
2. Confirmar que o menu aparece sem reset espontaneo.
3. Permanecer por pelo menos 2 minutos em cada menu principal.
4. Manter cada botao pressionado e confirmar que nao ocorre reset.
5. Permanecer aguardando conexao USB e confirmar que o WDT nao reinicia.

Resultado esperado: navegacao continua e nenhum loop normal dispara o WDT.

## ADC, zero e calibracao

1. Aplicar ADC estavel e comparar leitura bruta com media filtrada.
2. Variar AN0 em 1 ou 2 bits ao redor do zero.
3. Reduzir AN0 abaixo do zero registrado.
4. Aplicar degraus conhecidos e conferir 3 gramas por bit acima da zona morta.
5. Ligar segurando ESQUERDA, entrar na calibracao e confirmar ADC filtrado.
6. Pressionar CONFIRMA e verificar a mensagem de zero atualizado.
7. Pressionar RETORNA e confirmar a saida.

Resultado esperado: sem underflow, peso zero na zona morta e menor oscilacao.

## Botoes

1. Simular ruido/pulso menor que 30 ms em cada entrada.
2. Aplicar um pressionamento valido em CONFIRMA, RETORNA, ESQUERDA e DIREITA.
3. Manter o botao pressionado por 1 segundo.

Resultado esperado: pulsos curtos sao rejeitados e cada toque gera uma acao.

## EEPROM e compatibilidade

1. Carregar uma EEPROM real da v1.4 e iniciar o firmware.
2. Confirmar que checksum invalido apenas mostra aviso.
3. Abrir o historico das 12 glebas e comparar os 21 bytes com a v1.4.
4. Salvar uma avaliacao e verificar que somente bytes alterados sao gravados.
5. Confirmar checksum XOR de 0..251 no byte 252.
6. Corromper um byte entre 0 e 251 e reiniciar.
7. Executar limpeza e verificar a passagem pelos enderecos 0..255.
8. Confirmar que 0..251 ficam em `0xFF`, 252 recebe o checksum e 253..255
   ficam em `0xFF`.

Resultado esperado: dados antigos legiveis, aviso nao bloqueante e checksum
atualizado sem alterar o layout das glebas.

## Limites matematicos

1. Forcar espacamento entre plantas igual a zero na EEPROM.
2. Forcar espacamento entre ruas igual a zero.
3. Forcar renda e plantas por hectare iguais a zero.
4. Abrir os calculos de PRL, plantacao, produtividade e correcao.

Resultado esperado: nenhum travamento ou divisao por zero; resultado seguro 0.

## USB HID

1. Conectar RA5 e enumerar o dispositivo HID no computador.
2. Enviar comando com `in_data[0] != 0`.
3. Capturar exatamente 32 pacotes de 8 bytes.
4. Concatenar os pacotes e comparar com os 256 bytes da EEPROM.
5. Solicitar uma segunda resposta sem reiniciar o equipamento.
6. Confirmar que a segunda resposta tambem comeca no endereco zero.
7. Enviar comando com `in_data[0] == 0`.
8. Desconectar fisicamente durante o envio.

Resultado esperado: 256 bytes em ordem, mensagem `Dados enviados`, pacote
reiniciado em nova solicitacao e `usb_detach()` nas saidas.

## Regressao da interface

1. Percorrer avaliacao, historico, upload e limpeza.
2. Realizar uma avaliacao completa e consultar a gleba salva.
3. Comparar textos, setas e codigos dos botoes com a v1.4.

Resultado esperado: fluxo funcional e navegacao permanecem compativeis.
