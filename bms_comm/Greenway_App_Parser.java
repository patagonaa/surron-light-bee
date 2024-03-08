public void a(byte[] bArr) {
    String str;
    try {
        this.l.setDesignedcapacity(String.valueOf((bArr[0] & 255) | ((bArr[1] << 8) & 65535) | ((bArr[2] << 16) & 16777215) | ((bArr[3] << 24) & -1)) + " mAh");
        this.l.setDesignedVoltage(String.valueOf((bArr[4] & 255) | ((bArr[5] << 8) & 65535) | ((bArr[6] << 16) & 16777215) | ((bArr[7] << 24) & -1)) + " mV");
        this.l.setAutoOffTime(String.valueOf((bArr[8] & 255) | ((bArr[9] << 8) & 65535)) + " min");
        switch (bArr[10] & 255) {
            case 1:
                str = "长按关机";       // Long press to turn off
                break;
            case 2:
                str = "弱电开关控制";   // Weak switch control
                break;
            case 3:
                str = "弱电开关无休眠"; // weak switch no hibernation
                break;
            default:
                str = "常规模式";       // Regular mode
                break;
        }
        this.l.setOperateMode(str);
        this.l.setHardVersion(String.valueOf((bArr[11] & 240) >> 4) + "." + String.valueOf(bArr[11] & 15));
        String str2 = "";
        for (int i2 = 0; i2 < 4; i2++) {
            str2 = str2 + String.format("%c", new Object[]{Integer.valueOf(bArr[i2 + 12] & 255)});
        }
        this.l.setIDX(str2);
        this.l.setManufactDate(String.format("%d-%d-%d", new Object[]{Integer.valueOf(bArr[16] + 2000), Byte.valueOf(bArr[17]), Byte.valueOf(bArr[18])}));
        String str3 = "";
        for (int i3 = 0; i3 < 16; i3++) {
            str3 = str3 + String.format("%c", new Object[]{Byte.valueOf(bArr[i3 + 32])});
        }
        this.l.setManufactor(str3.trim());
        String str4 = "";
        for (int i4 = 0; i4 < 16; i4++) {
            str4 = str4 + String.format("%c", new Object[]{Byte.valueOf(bArr[i4 + 48])});
        }
        this.l.setCellName(str4.trim());
        String str5 = "";
        for (int i5 = 0; i5 < 32; i5++) {
            str5 = str5 + String.format("%c", new Object[]{Byte.valueOf(bArr[i5 + 64])});
        }
        this.l.setBatteryName(str5.trim());
        String str6 = "";
        for (int i6 = 0; i6 < 32; i6++) {
            str6 = str6 + String.format("%c", new Object[]{Byte.valueOf(bArr[i6 + 96])});
        }
        this.l.setBarCode(str6.trim());
        this.l.setOverVoltageValue(String.valueOf((bArr[208] & 255) | ((bArr[209] << 8) & 65535)) + " mV");
        this.l.setOverVoltageReleaseValue(String.valueOf((bArr[210] & 255) | ((bArr[211] << 8) & 65535)) + " mV");
        String str7 = "";
        switch (bArr[212] & 3) {
            case 0:
                str7 = "1 S";
                break;
            case 1:
                str7 = "2 S";
                break;
            case 2:
                str7 = "4 S";
                break;
            case 3:
                str7 = "8 S";
                break;
        }
        this.l.setOverVoltageReleaseTime(str7);
        String str8 = "";
        switch ((bArr[212] & 48) >> 4) {
            case 0:
                str8 = "1 S";
                break;
            case 1:
                str8 = "2 S";
                break;
            case 2:
                str8 = "4 S";
                break;
            case 3:
                str8 = "8 S";
                break;
        }
        this.l.setOverVoltageDelayTime(str8);
        this.l.setUnderVoltageValue(String.valueOf((bArr[213] & 255) | ((bArr[214] << 8) & 65535)) + " mV");
        this.l.setUnderVoltageReleaseValue(String.valueOf((bArr[215] & 255) | ((bArr[216] << 8) & 65535)) + " mV");
        String str9 = "";
        switch ((bArr[212] & 12) >> 2) {
            case 0:
                str9 = "1 S";
                break;
            case 1:
                str9 = "2 S";
                break;
            case 2:
                str9 = "4 S";
                break;
            case 3:
                str9 = "8 S";
                break;
        }
        this.l.setUnderVoltageReleaseTime(str9);
        String str10 = "";
        switch ((bArr[212] & 192) >> 6) {
            case 0:
                str10 = "1 S";
                break;
            case 1:
                str10 = "2 S";
                break;
            case 2:
                str10 = "4 S";
                break;
            case 3:
                str10 = "8 S";
                break;
        }
        this.l.setUnderVoltageDelayTime(str10);
        String str11 = "";
        switch ((bArr[217] & 192) >> 6) {
            case 0:
                str11 = "BQ76930";
                break;
            case 1:
                str11 = "BQ76940";
                break;
            case 2:
                str11 = "BQ76925";
                break;
        }
        this.l.setProtectionChipModle(str11);
        this.l.setBatterySerial(String.valueOf(bArr[217] & 63));
        String str12 = "";
        switch ((bArr[218] & 24) >> 3) {
            case 0:
                str12 = "70 us";
                break;
            case 1:
                str12 = "100 us";
                break;
            case 2:
                str12 = "200 us";
                break;
            case 3:
                str12 = "400 us";
                break;
        }
        this.l.setShortCircuitCurrentConfigureTime(str12);
        String str13 = "";
        switch (bArr[218] & 7) {
            case 0:
                str13 = "44 mV";
                break;
            case 1:
                str13 = "67 mV";
                break;
            case 2:
                str13 = "89 mV";
                break;
            case 3:
                str13 = "111 mV";
                break;
            case 4:
                str13 = "133 mV";
                break;
            case 5:
                str13 = "155 mV";
                break;
            case 6:
                str13 = "178 mV";
                break;
            case 7:
                str13 = "200 mV";
                break;
        }
        this.l.setShortCircuitCurrentConfigureCurrent(str13);
        String str14 = "";
        switch ((bArr[219] & 128) >> 7) {
            case 0:
                str14 = "断载且过恢时"; // Broken load and over-resumed (Resume after disconnect?)
                break;
            case 1:
                str14 = "超时则恢复";   // Resume after timeout
                break;
        }
        this.l.setShortCircuitReleaseConfigure(str14);
        this.l.setShortCircuitReleaseTime(String.valueOf(bArr[219] & Byte.MAX_VALUE) + " S");
        String str15 = "";
        switch ((bArr[220] & 112) >> 4) {
            case 0:
                str15 = "8 ms";
                break;
            case 1:
                str15 = "20 ms";
                break;
            case 2:
                str15 = "40 ms";
                break;
            case 3:
                str15 = "80 ms";
                break;
            case 4:
                str15 = "160 ms";
                break;
            case 5:
                str15 = "320 ms";
                break;
            case 6:
                str15 = "640 ms";
                break;
            case 7:
                str15 = "1280 ms";
                break;
        }
        this.l.setOverCurrentConfigureTime(str15);
        String str16 = "";
        switch (bArr[220] & 15) {
            case 0:
                str16 = "17 mV";
                break;
            case 1:
                str16 = "22 mV";
                break;
            case 2:
                str16 = "28 mV";
                break;
            case 3:
                str16 = "33 mV";
                break;
            case 4:
                str16 = "39 mV";
                break;
            case 5:
                str16 = "44 mV";
                break;
            case 6:
                str16 = "50 mV";
                break;
            case 7:
                str16 = "56 mV";
                break;
            case 8:
                str16 = "61 mV";
                break;
            case 9:
                str16 = "67 mV";
                break;
            case 10:
                str16 = "72 mV";
                break;
            case 11:
                str16 = "78 mV";
                break;
            case 12:
                str16 = "83 mV";
                break;
            case 13:
                str16 = "89 mV";
                break;
            case 14:
                str16 = "94 mV";
                break;
            case 15:
                str16 = "100 mV";
                break;
        }
        this.l.setOverCurrentConfigureCurrent(str16);
        this.l.setOverCurrentReleaseTime(String.valueOf(bArr[221] & 255) + " S");
        this.l.setCHGOverTemperatureValue(String.valueOf(((bArr[222] & 15) * 2) + 40) + " ℃");
        this.l.setDSGOverTemperatureValue(String.valueOf((((bArr[222] & 240) >> 4) * 2) + 40) + " ℃");
        this.l.setCHGOverTemperatureReleaseValue(String.valueOf(((bArr[223] & 15) * 2) + 40) + " ℃");
        this.l.setDSGOverTemperatureReleaseValue(String.valueOf((((bArr[223] & 240) >> 4) * 2) + 40) + " ℃");
        this.l.setCHGOverTemperatureDelayTime(String.valueOf(bArr[224] & 15) + " S");
        this.l.setDSGOverTemperatureDelayTime(String.valueOf((bArr[224] & 240) >> 4) + " S");
        this.l.setCHGUnderTemperatureValue(String.valueOf(10 - ((bArr[225] & 15) * 2)) + " ℃");
        this.l.setDSGUnderTemperatureValue(String.valueOf(10 - (((bArr[225] & 240) >> 4) * 2)) + " ℃");
        this.l.setCHGUnderTemperatureReleaseValue(String.valueOf(10 - ((bArr[226] & 15) * 2)) + " ℃");
        this.l.setDSGUnderTemperatureReleaseValue(String.valueOf(10 - (((bArr[226] & 240) >> 4) * 2)) + " ℃");
        this.l.setCHGUnderTemperatureDelayTime(String.valueOf(bArr[227] & 15) + " S");
        this.l.setDSGUnderTemperatureDelayTime(String.valueOf((bArr[227] & 240) >> 4) + " S");
        this.l.setChargeBalanceVoltage(String.valueOf((bArr[228] & 255) | ((bArr[229] << 8) & 65535)) + " mV");
        this.l.setDischargeBalanceCurrent(String.valueOf((bArr[230] & 255) * 100) + " mA");
        this.l.setBalanceSetupValue(String.valueOf(bArr[231] & 255) + " mV");
        this.l.setChargeCurrentValue(String.valueOf((bArr[232] & 255) * 100) + " mA");
        this.l.setChargeCurrentDelayTime(String.valueOf(bArr[233] & 255) + " S");
        this.l.setChargeCurrentReleaseTime(String.valueOf(bArr[234] & 255) + " S");
        this.l.setPrimaryOverDischargeValue(String.valueOf(bArr[235]) + " A");
        this.l.setPrimaryOverDischargeDelayTime(String.valueOf(bArr[236] & 255) + " S");
        this.l.setPrimaryOverDischargeReleaseTime(String.valueOf(bArr[237] & 255) + " S");
        this.l.setSampleResisterValue(String.valueOf((bArr[238] & 255) | ((bArr[239] << 8) & 65535)) + " uΩ");
        this.l.setProtectICCurrentStandard(String.valueOf(bArr[240] & 255) + "mA");
        this.l.setPV(String.valueOf((bArr[429] & 255) | ((bArr[430] << 8) & 65535)) + " mV");
        String str17 = "";
        for (int i7 = 0; i7 < Integer.parseInt(this.l.getBatterySerial()); i7++) {
            str17 = str17 + String.format("%d: %d mV \n\r", new Object[]{Integer.valueOf(i7 + 1), Byte.valueOf(bArr[i7 + 304])});
        }
        this.l.setVoltageOffSet(str17);
    } catch (Exception e2) {
        e2.printStackTrace();
    }
}