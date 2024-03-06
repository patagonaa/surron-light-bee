# RS485 communication

RS485 communication is done at 9600 baud 8N1.

All messages seem to have the same structure:
- Command (`46` = request, `47` = response, `57` = unsolicited)
- Address (4 bytes)
- Data (0 - n bytes)
- Checksum (1 byte = sum of all previous bytes)

Full message dumps of the bike (sniffed at the battery BMS connection) are available under `dumps`

## Addresses
### 16010701
Request:
- `461601070165`

Response:
- `4716010701056B`

### 16010806
Request:
- `46160108066B`

Response:
- `471601080610100F001111BD`
- `47160108060F100F001111BC`

### 16010904
Request:
- `46160109046A`

Response:
- `47160109046BF20000C8`
- `47160109046AF20000C7`
- `471601090468F20000C5`
- `471601090469F20000C6`
- `471601090458F20000B5`
- `471601090454F20000B1`
- `47160109042FF200008C`
- `47160109045DF20000BA`

Fields
- Byte 0-4: Command + Address
- Byte 5-8: Battery voltage (uint32 `63F20000` => 62.051V)

(Plot of `output_2024-03-05_22-15-39_standup_gas_standown.log`)
![](./plot_16010904.png)

### 16010D01
Request:
- `4616010D016B`

Response:
- `4716010D014BB7`

### 16011609
Request:
- `46160116097C`

Response:
- `4716011609E0030000000000000060`

### 8301480C
No request, is sent continuously:

- `578301480C0000000000000080000000AF`
- `578301480C0000000000000081000000B0`
- `578301480C4B63F200000000800000004F`
- `578301480C4B58F20000000000000000C4`
- `578301480C4B49F20000000000000000B5`
- `578301480C4B51F200000000800000003D`

Fields
- Byte 0-4: Command + Address
- Byte 5: Battery percent?
- Byte 6-9: Battery voltage (uint32 `63F20000` => 62.051V)
- Byte 12: Error/Status Flags? (`81` with missing battery, `80` with kickstand down, `00` with kickstand up)

(Plot of `output_2024-03-05_22-15-39_standup_gas_standown.log`)
![](./plot_8301480C.png)

### 83014B02
No request, is sent continuously:

- `5783014B020129`
- `5783014B020830`
- `5783014B020028`

(Plot of `output_2024-03-05_22-04-43_75percent_62.0V_5265km.log`)
![](./plot_83014B02.png)