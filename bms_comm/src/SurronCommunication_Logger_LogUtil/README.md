# LogUtil
As of now (while there is no working SD card support for ESP32-S3), the only way to get the log files out of the ESP is using `esptool` and `mkspiffs`.

As nanoFramework overrides the default SPIFFS parameters, `mkspiffs` has to be compiled yourself, something like `make dist CPPFLAGS="-DSPIFFS_OBJ_NAME_LEN=256 -DSPIFFS_OBJ_META_LEN=4"` (see https://github.com/nanoframework/nf-interpreter/blob/12abaab58412cd0208fd0d09293eda0fa4e10426/targets/ESP32/_IDF/sdkconfig.default.esp32s3#L1012-L1021).

After also looking up the offset and length of the spiffs partition of your particular ESP32 variant (e.g. https://github.com/nanoframework/nf-interpreter/blob/12abaab58412cd0208fd0d09293eda0fa4e10426/targets/ESP32/_IDF/esp32s3/partitions_nanoclr_8mb.csv#L14), the files can be extracted using:

```bash
# put ESP32 into boot mode by holding "boot" button while pressing "reset" button
esptool --port COM15 read_flash 0x490000 0x300000 spiffs.bin
mkspiffs -u ./out -b 4096 -p 512 ./spiffs.bin
```