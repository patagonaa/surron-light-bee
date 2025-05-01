# Stock Battery Info - 32Ah
As mentioned in the main readme, the 32Ah stock battery is comprised of a 16s11p configuration with Panasonic NCR18650PF[^ncr18650_datasheet] cells.

These cells are rated at 2900mAh typical capacity with a maximum discharge current of 10A, giving the aforementioned capacity and maximum theoretical safe pack current of 110A


## Construction
The Panasonic 32Ah pack is held *firmly* in place vertically using *very* strong adhesive. This adhesive sucks to deal with, but as long as you use a stiff, preferably somewhat sharp <ins>***non conductive***</ins> implement, you can slice through the top adhesive. Then, to remove the pack I personally put M3 screws into the center unused inserts and tied a nylon cord around them and *pulled like hell*. The inserts amazingly held and I was able to slide the pack out with much effort. Not really recommended, but you do you. If you are attempting this, make sure the PCB side is facing up and ensure when it is partially out to not short against the casing. Cover your main terminals with kapton tape before doing any of this. <ins>**Do not disassemble this pack if you don't know exactly what you are doing! Nearly 2 kilowatt-hours of energy stored in such a small package is no joke! It can and will hurt you and your surroundings if mishandled.**</ins>

The pack uses injection molded cell holders that hold each cell separated by a small distance. Inbetween the halves and on the outside sides, there are custom cut epoxy glass resin panels for insulation. The halves are physically held together via 8 long bolts. Inside the casing, there is nothing apart from a thin neoprene pad to protect the bottom of the pack. The pack is also held firm horizontally by several blocks of stiff foam attached to the side panels.

Electrically, the pack is split into two 8s halves. Current flows from one half, down and then into 6 12awg wires on the bottom which join the halves. Current then comes up the other side and out. These wires are soldered on and recessed into the pack cell holders.
![Front](/images/panasonic_32ah/panasonic_18650_32ah_front.jpg)
![Back](/images/panasonic_32ah/panasonic_18650_32ah_back.jpg)

A question I had when handling this pack for the first time was "What the heck is that sand sound inside?" Thankfully as I suspected, it was just dessicant packs stuffed inside for moisture control.

### Balance Pinout
This view and the text direction match the values of the pins of the PCB. B0 (aka B- is not connected though is present and on the PCB. Battery tab is not connected, but it is present and folded back behind the side epoxy glass panel). In addition, the far right pin is also not connected.

Connectors on the PCB are Molex 502352-1100[^digikey_molex_11pf] and 502352-0700[^digikey_molex_7pf]. The female cnnectors on the harness are Molex 502351-1100[^digikey_molex_11pm] and 502351-0700[^digikey_molex_7pm]. You can get pre-crimped wires here[^digikey_molex_female_wire]
![PCB Balance pinout](/images/panasonic_32ah/balance_pinout.png)

### Thermistor Routing
I cannot confirm a consistent routing position nor which pairs on the connector go to which thermistor. Also be careful with the thermistor harness, the wires are just taped on and can be pulled out. The thermistor bulbs are also typically fragile and could break being removed from the white thermal adhesive used, though when I pulled mine out (no longer needed) they stayed intact.
![Thermistor routing illustrated](/images/panasonic_32ah/panasonic_18650_32ah_temp_sensor_layout.jpg)

### Thermistor Pinout
Starting from either side, neighboring pins go to one thermistor, four total. The connector is a Molex 502351-0800[^digikey_molex_8pf].

![BMS top PCB bottom](/images/panasonic_32ah/ntc_therm_harness.jpg)

## BMS
The Greenway BMS consists of two PCBs, one control and interface PCB and another current handling MOSFET PCB. Typically, people attempt to repair the BMS by replacing the top PCB. While this may work, the issue could just as well be a bad cell/group, broken balance wire or tab (The latter of which I experienced), or the bottom PCB bad as well.

Little side note: if you are desoldering from the MOSFET PCB, remove the heatsink from the back and crank your iron temperature, you'll need it since it's an aluminum backed board which sucks heat away fast.

### Photos

![BMS top PCB top annotated](/images/panasonic_32ah/surron_bms_top_top_annotated.png)
![BMS top PCB bottom](/images/panasonic_32ah/surron_bms_top_rear.png)
![BMS bottom PCB top](/images/panasonic_32ah/surron_bms_bottom_top.png)
![BMS bottom PCB bottom](/images/panasonic_32ah/surron_bms_bottom_rear.png)

[^ncr18650_datasheet]: Panasonic NCR18650PF Datasheet https://web.archive.org/web/20250124024705/http://www.kinstarbattery.com/Uploads/Celldatasheet/20201228015500_Panasonic_NCR18650PF_Datasheet.pdf

[^digikey_molex_8pf]: 8 pin Molex DuraClik Female Connector https://www.digikey.com/en/products/detail/molex/5023510800/2818944

[^digikey_molex_7pf]: 7 pin Molex DuraClik Female Connector https://www.digikey.com/en/products/detail/molex/5023510700/4555380?s=N4IgTCBcDaIKwAYwGY4EYEHYEJAXQF8g

[^digikey_molex_11pf]: 11 pin Molex DuraClik Female Connector https://www.digikey.com/en/products/detail/molex/5023511100/5700408?s=N4IgTCBcDaIKwAYwGY4EYMISAugXyA

[^digikey_molex_7pm]: 7 pin Molex DuraClik Male Connector https://www.digikey.com/en/products/detail/molex/5023521100/5700426?s=N4IgTCBcDaIKwAYwGY5gIzoQkBdAvkA

[^digikey_molex_11pm]: 11 pin Molex DuraClik Male Connector https://www.digikey.com/en/products/detail/molex/5023520700/4555385?s=N4IgTCBcDaIKwAYwGY5gQdgQkBdAvkA

[^digikey_molex_female_wire]: Pre-crimped female DuraClik pin for the female connectors https://www.digikey.com/en/products/detail/molex/0797581008/6564338?s=N4IgTCBcDaIOwE44FYAcBGADJ1IC6AvkA