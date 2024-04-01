# RS485 communication

RS485 communication is done at 9600 baud 8N1.

The BMS seems to have two sleep modes:

- RS485 off
    - happens after around 3 seconds, RS485 termination/pullup/pulldown is turned off and BMS takes some time / a few tries until it responds
    - can be reenabled via RS485 communication, 60V input, button press or battery charging
- standby
    - happens after some minutes/hours
    - can be reenabled with 60V input, button press or battery charging (though not via RS485)

Oddities:
- When the BMS display is on (60V input, button pressed or charging), the BMS takes pretty long to respond sometimes (normal: ~10ms, when display is on: 80ms, maybe longer), probably while the microcontroller is busy updating the display.
- When waking the BMS up (even just from the ~3s RS485 timeout), The RTC value is outdated and only changes to the correct value after a second or so.
- BMS only seems to update its internal values every second or so, polling more often than that works, but does not really do anything.

## Message Structure

All messages seem to have the same structure:
- Command (`46` = request, `47` = response, `57` = unsolicited)
- Address (2 bytes)
- Parameter id (1 byte)
- Parameter length (1 byte)
- Data (0 - n bytes)
- Checksum (1 byte = sum of all previous bytes)

Full message dumps of the bike (sniffed at the battery BMS connection) are available under `./dumps`

Example code to work with the Surron RS485 bus is available under `./src`

My theory is, that the controller/ESC is the bus controller (only bus member that can send without being asked to), the battery responds to requests and the display only reads.
Points in case:
- responses (`47`) stop when battery is detached (requests and unsolicited messages keep on going)
- unsolicited messages (`57`) contain the data received in the responses with a slight delay
- unsolicited messages (`57`) contain multiple parameters (like battery percentage, battery voltage, status, etc.), which suggest those messages are aimed at the display.

## Commands
## `46` (Request)
request for a value / list of values
- byte 0: command
- byte 1-2: address
- byte 3: parameter
- byte 4: data length
- last byte: checksum 

| cmd  | addr   | param | len  | chk  |
|------|--------|-------|------|------|
| `46` | `1601` | `07`  | `01` | `65` |
| `46` | `1601` | `08`  | `06` | `6B` |
| `46` | `1601` | `09`  | `04` | `6A` |
| `46` | `1601` | `0D`  | `01` | `6B` |

## `47` (Response)
response to `46`
- byte 0: command
- byte 1-2: address
- byte 3: parameter
- byte 4: data length
- bytes n: data
- last byte: checksum

| cmd  | addr   | param | len  | data           | chk  |
|------|--------|-------|------|----------------|------|
| `47` | `1601` | `07`  | `01` | `05`           | `65` |
| `47` | `1601` | `08`  | `06` | `10100F001111` | `BD` |
| `47` | `1601` | `09`  | `04` | `6BF20000`     | `C8` |
| `47` | `1601` | `0D`  | `01` | `4B`           | `B7` |

## `57` (Unsolicited Response)
- byte 0: command
- byte 1-2: address
- byte 3: parameter
- byte 4: data+checksum length
- bytes n: data
- last byte: checksum

| cmd  | addr   | param | len  | data                     | chk  |
|------|--------|-------|------|--------------------------|------|
| `57` | `8301` | `48`  | `0C` | `0000000000000080000000` | `AF` |
| `57` | `8301` | `48`  | `0C` | `4B63F20000000080000000` | `4F` |
| `57` | `8301` | `4B`  | `02` | `00`                     | `28` |

## Parameter Reverse Engineering
### Bike sniffing (see dumps under `./dumps`)

#### Requests (`46`/`47`) from ESC to BMS (addr `1601`)

| param id  | length | response data        |
|-----------|--------|----------------------|
| `07` / 7  | 1      | `05`                 |
| `08` / 8  | 6      | `151515001616`       |
| `09` / 9  | 4      | `6AF20000`           |
| `0D` / 13 | 1      | `4B`                 |
| `16` / 22 | 9      | `E00300000000000000` |


#### Unsolicited Data (`57`) from ESC to Display? (addr `8301`)
| param id  | length | data                     |
|-----------|--------|--------------------------|
| `48` / 72 | 11     | `4B6BF20000000080000000` |
| `4B` / 75 | 1      | `01`                     |

##### `48` / 72:
- Byte 0: Battery percent (`4B` => 75%)
- Byte 1-4: Battery voltage (uint32 `63F20000` => 62.051V)
- Byte 5: ???
- Byte 6: Brake status?? (usually `00`, can be `02`)
- Byte 7: Error/Status Flags? (`81` with missing battery, `80` with kickstand down, `00` with kickstand up)
- Bytes 8-10: ???

##### `4B` / 75:
probably the way config changes (via brake switch) are communicated to display:
- normal:  `01` -> `08` -> `00`
- regen 1?: `01` -> `10` -> `20` -> `00`
- regen 2?: `01` -> `08` -> `10` -> `00`
- regen 3?: `01` -> `20` -> `08` -> `00`

### BMS read brute force
Just requesting all parameter IDs from battery with `461601XXXXXX` (The BMS does not respond to parameter IDs that don't exist).

Length can be set up to 64 (though param 160 only responds up to around 32).
When requesting more data than is actually in the field, it seems like the BMS reads past its buffer and subsequent params are returned.

Lengths can be determined somewhat by counting bytes until the next param appears in the data.

### Torp TC500
There is an aftermarket ESC called "Torp TC500", which has the Surron RS485 communication implemented and which has an app that shows the battery parameters, so we can at least know which parameters there are (random screenshot from Google Images):

<img src="./Torp-TC500-app-screenshot.jpg" width="200"></img>

| Parameter Name                     | Parameter ID                    |
|------------------------------------|---------------------------------|
| RTC                                | 29                              |
| Serial Number                      | 35                              |
| Manufacturing Date                 | 27                              |
| Hardware Version                   | 26                              |
| Software Version                   | 26                              |
| Full capacity                      | 16                              |
| Remain capacity                    | 15                              |
| Charging Cycles                    | 23                              |
| Estimated mileage                  | 21                              |
| State of health                    | 14                              |
| Temperatures                       | 8 (exact mapping still unknown) |
| Error/Warning Flags                | 22                              |
| History (max/min temp, curr, volt) | 38                              |
| Protection IC error counter        | ???                             |
| Battery drop counter               | ???                             |

### Greenway BMS App

There is an Android app from the BMS manufacturer that can be found [here](https://imb.greenway-battery.com/assets/imb/page/download.html) (Direct link!).

The Surron BMS does not have Bluetooth so it can't be used with the app, however it looks like the parameter IDs and meaning are the same.

A search for `GW_RamArrayOrder` in the decompiled code finds the places where data is requested (including the parameter IDs and lengths) and leads to the code that handles the data.

## BMS Parameter Map

Unsure lengths/descriptions are marked with `?`. More question marks = more uncertainty. The address is always `1601` / 278 for talking to the BMS.

| param | len  | data                                                               | desc                                                 |
|------:|------|--------------------------------------------------------------------|------------------------------------------------------|
|     0 | 4    | `46000000`                                                         | (something about firmware upgrade status in app)     |
|     7 | 1    | `05`                                                               | ?                                                    |
|     8 | 8    | `1515150016161600`                                                 | Temperatures (see [below](#temperatures))            |
|     9 | 4    | `63F20000`                                                         | Battery Voltage (`63F20000` uint32/1000 => 62.051V)  |
|    10 | 4    | `00000000`/`10000000`/`F0FFFFFF`/`B7FAFFFF`                        | Battery Current (`B7FAFFFF` int32/1000 => -1.353A)   |
|    13 | 1    | `4B`                                                               | Battery Percent (`4B` => 75%)                        |
|    14 | 1    | `64`                                                               | Battery Health (`64` => 100%)                        |
|    15 | 4    | `CA680000`                                                         | Remaining Capacity (`13680000` uint32 => 26643mAh)   |
|    16 | 4    | `128B0000`                                                         | Total Capacity (`128B0000` uint32 => 35602mAh)       |
|    17 | <=2? | `4000`                                                             | ??? (`4000` uint16 => 64)                            |
|    20 | 4    | `80027F32` / `0102B0EE` / `00027936` / `01025C36` /  `00023F36`    | ??? (read as 2x uint16 in app, but unused)           |
|    21 | 12   | `128B000044E226001A7C0000`                                         | Statistics (see [below](#statistic-bytes))           |
|    22 | 10   | `E0030000000000000000`/`20000000000000000000`                      | BMS Status (see [below](#bms-status))                |
|    23 | 4    | `4E000000` / `4F000000`                                            | Charging Cycles (`4E000000` uint32 => 78)            |
|    24 | 4    | `A4880000`                                                         | Designed Capacity (`A4880000` uint32 => 31980mAh)    |
|    25 | 4    | `00E10000`                                                         | Designed Voltage (`00E10000` uint32/1000 => 57.600V) |
|    26 | 8    | `0E03000055343237`                                                 | SW ver (`0E` `03` => 3.14), HW ver, ASCII FW Index   |
|    27 | 3    | `160301`                                                           | Manufacture Date (2022-03-01)                        |
|    28 | 4    | `00000000`                                                         | ?                                                    |
|    29 | 6    | `180307062F02`                                                     | RTC time (`180307062F02` => 2024-03-07T06:47:02)     |
|    30 | 6    | `000071032201`/`000071032301`                                      | ??? (uint16, uint16) (something `chargeTime` in app) |
|    32 | 16   | `475245454E5741590000000000000000`                                 | BMS Manufacturer (ASCII "GREENWAY")                  |
|    33 | 32   | `444D373331363131000000000000000000000000000000000000000000000000` | Battery Model? (ASCII "DM731611")                    |
|    34 | 16   | `4E435231383635304244000000000000`                                 | Cell Type (ASCII "NCR18650BD")                       |
|    35 | 32   | `3074313858303633313136393032323236000000000000000000000000000000` | Serial Number (ASCII "0t18X063116902226")            |
|    36 | 32   | `280F230F230F230F230F280F290F290F280F290F270F290F290F2A0F270F2D0F` | Cell voltages (`280F` uint16/1000 => 3.880V)         |
|    37 | 32   | `0000000000000000000000000000000000000000000000000000000000000000` | Cell voltages (cont.)                                |
|    38 | 14   | `4DA7FEFFF93C000080100C0C3302`                                     | History (see [below](#history-bytes))                |
|    39 | ???  | `FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF0200FFFFFFFFFFFFFFFFFFFF0900FFFF` | ?                                                    |
|    48 | ???  | `0000000000000000000000000000000000000000000000000000000000000000` | ?                                                    |
|   120 | ???  | `0000000000000000000000000000000000000000000000000000000000000000` | ?                                                    |
|   160 | ???  | `20000000000000004B647AF20000F0FFFFFF1515161600004E00000000000000` | ? (does not respond with length >32)                 |

### Temperatures
ESC reads 6 bytes, however according to app it's 8 bytes long (only bytes 0, 1, 2, 4, 5, 6 are plausible values though).
Also, the app seems to be a bit buggy there as well.
My best guess:
- byte 0-2: Cell Temperatures (`15` sbyte => 21°C)
- byte 4: Discharge MOSFET Temperature? (`16` sbyte => 22°C)
- byte 5: Charge MOSFET Temperature? (`16` sbyte => 22°C)
- byte 6: Soft-Start Circuit Temperature? (`16` sbyte => 22°C)

Though for me, bytes 4 and 5 were always equal, so maybe there is only one temperature sensor there.

"soft start" (also referred to as "pre-start" or "pre-charge" in the app) is most likely a built-in BMS feature that reduces the initial current surge when plugging a device into the battery ("anti spark").

### History Bytes
Example: `4DA7FEFFF93C000080100C0C3302`
- byte 0-3: Max Discharge Current (`4DA7FEFF` int32/1000 => -88.243A)
- byte 4-7: Max Charge Current (`F93C0000` int32/1000 => 15.609A)
- byte 8-9: Max Cell Voltage (`8010` uint16/1000 => 4.224V)
- byte 10-11: Min Cell Voltage (`0C0C` uint16/1000 => 3.084V)
- byte 12: Max Temperature (`33` sbyte => 51°C)
- byte 13: Min Temperature (`02` sbyte => 2°C)

### Statistic Bytes
- byte 0-3: Total Capacity (`128B0000` uint32 => 35_602mAh)
- byte 4-7: Total Capacity Charged (`44E22600` uint32 => 2_548_292mAh)
- byte 8-11: Capacity Charged in this Cycle (`1A7C0000` uint32 => 31_770mAh)

### BMS Status
- byte 0-1: ???
- byte 2-5: Error Flags
- byte 6-9: Warning Flags