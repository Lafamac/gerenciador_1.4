# Gerenciador de Colheita para Windows

Aplicativo WinForms para o Gerenciador de Colheita com PIC18F2550.

## Funcionalidades

- Localiza o dispositivo HID com VID `04D8` e PID `0011`.
- Permite selecionar firmware `1.5.3` ou `1.6` na mesma tela.
- Usa o comando legado `1` para a versão 1.5.3 e o comando com checksum `2`
  para a versão 1.6.
- Recebe 32 pacotes HID de 8 bytes.
- Valida o checksum XOR dos bytes 0 a 251.
- Interpreta as 12 glebas de 21 bytes.
- Interpreta os campos da versao 1.6, incluindo freio, vibracao e diagnostico.
- Salva a imagem bruta da EEPROM em `.bin`.
- Exporta os dados em `.csv`.
- Gera um relatório `.pdf` sem bibliotecas externas.
- Para a versão 1.5.3, reproduz o relatório de duas páginas e duas colunas
  usado pelo programa antigo.

## Protocolo

- Comando `0`: desconectar.
- Comando `1`: download legado; o byte 252 é transmitido como `0xFF`.
- Comando `2`: download novo; o byte 252 contém o checksum real.
- Resposta: 256 bytes em 32 relatórios de 8 bytes, na ordem dos endereços 0 a 255.

No Windows, a API HID acrescenta o Report ID zero, portanto cada leitura e escrita
do programa usa 9 bytes: um byte de Report ID seguido por 8 bytes do firmware.

O primeiro pacote pode levar até 6 segundos. Em caso de falha, o programa informa
qual dos 32 pacotes não foi recebido e lembra que o equipamento deve estar na tela
`USB conectado!`.

## Compilação

Execute `build.cmd`. Ele usa o compilador C# do .NET Framework presente no
Windows e cria:

```text
dist\GerenciadorColheita.exe
```

Também é possível abrir `GerenciadorColheita.csproj` no Visual Studio ou executar:

```powershell
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' `
  GerenciadorColheita.csproj /p:Configuration=Release
```

O executável será criado em `bin\Release\GerenciadorColheita.exe`.
