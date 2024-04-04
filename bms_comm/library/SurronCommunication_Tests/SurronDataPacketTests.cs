using SurronCommunication;
using SurronCommunication.Packet;

namespace SurronCommunication_Tests
{
    public class SurronDataPacketTests
    {
        [Test]
        public void FromBytes_InvalidChecksum_Throws()
        {
            Assert.Throws<InvalidDataException>(() => { SurronDataPacket.FromBytes(HexUtils.HexToBytes("4616010701FF")); });
        }
        [Test]
        public void FromBytes_Short_Throws()
        {
            Assert.Throws<ArgumentException>(() => { SurronDataPacket.FromBytes(HexUtils.HexToBytes("4616")); });
        }

        [Test]
        public void FromBytes_Valid_46()
        {
            var packet = SurronDataPacket.FromBytes(HexUtils.HexToBytes("461601070165"));

            Assert.That(packet.Command, Is.EqualTo(SurronCmd.ReadRequest));
            Assert.That(packet.Address, Is.EqualTo(0x0116));
            Assert.That(packet.Parameter, Is.EqualTo(0x07));
            Assert.That(packet.DataLength, Is.EqualTo(1));
        }

        [Test]
        public void FromBytes_Valid_47()
        {
            var packet = SurronDataPacket.FromBytes(HexUtils.HexToBytes("4716010701056B"));

            Assert.That(packet.Command, Is.EqualTo(SurronCmd.ReadResponse));
            Assert.That(packet.Address, Is.EqualTo(0x0116));
            Assert.That(packet.Parameter, Is.EqualTo(0x07));
            Assert.That(packet.DataLength, Is.EqualTo(1));
            CollectionAssert.AreEqual(new byte[] { 0x05 }, packet.CommandData);
        }

        [Test]
        public void FromBytes_Valid_57()
        {
            var packet = SurronDataPacket.FromBytes(HexUtils.HexToBytes("578301480C4B63F200000000800000004F"));

            Assert.That(packet.Command, Is.EqualTo(SurronCmd.Status));
            Assert.That(packet.Address, Is.EqualTo(0x0183));
            Assert.That(packet.Parameter, Is.EqualTo(0x48));
            Assert.That(packet.DataLength, Is.EqualTo(11));
            CollectionAssert.AreEqual(HexUtils.HexToBytes("4B63F20000000080000000"), packet.CommandData);
        }

        [Test]
        public void ToBytes_46()
        {
            var packet = SurronDataPacket.Create(SurronCmd.ReadRequest, 0x116, 0x07, 1, null);
            CollectionAssert.AreEqual(HexUtils.HexToBytes("461601070165"), packet.ToBytes());
        }

        [Test]
        public void ToBytes_47()
        {
            var packet = SurronDataPacket.Create(SurronCmd.ReadResponse, 0x116, 0x07, 1, [0x05]);
            CollectionAssert.AreEqual(HexUtils.HexToBytes("4716010701056B"), packet.ToBytes());
        }

        [Test]
        public void ToBytes_57()
        {
            var packet = SurronDataPacket.Create(SurronCmd.Status, 0x183, 0x48, 11, HexUtils.HexToBytes("4B63F20000000080000000"));
            CollectionAssert.AreEqual(HexUtils.HexToBytes("578301480C4B63F200000000800000004F"), packet.ToBytes());
        }
    }
}