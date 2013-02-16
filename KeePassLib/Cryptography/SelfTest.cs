/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2013 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;

using KeePassLib.Cryptography.Cipher;
using KeePassLib.Keys;
using KeePassLib.Native;
using KeePassLib.Utility;
using KeePassLib.Resources;
using KeePassLib.Security;

namespace KeePassLib.Cryptography
{
/* #pragma warning disable 1591
	/// <summary>
	/// Return values of the <c>SelfTest.Perform</c> method.
	/// </summary>
	public enum SelfTestResult
	{
		Success = 0,
		RijndaelEcbError = 1,
		Salsa20Error = 2,
		NativeKeyTransformationError = 3
	}
#pragma warning restore 1591 */

	/// <summary>
	/// Class containing self-test methods.
	/// </summary>
	public static class SelfTest
	{
		/// <summary>
		/// Perform a self-test.
		/// </summary>
		public static void Perform()
		{
			TestFipsComplianceProblems(); // Must be the first test

			TestRijndael();
			TestSalsa20();
			
			TestNativeKeyTransform();
			
			TestHmacOtp();

			TestProtectedObjects();
			TestMemUtil();
			TestStrUtil();
			TestUrlUtil();

			Debug.Assert((int)PwIcon.World == 1);
			Debug.Assert((int)PwIcon.Warning == 2);
			Debug.Assert((int)PwIcon.BlackBerry == 68);
		}

		internal static void TestFipsComplianceProblems()
		{
#if !KeePassRT
			try { new RijndaelManaged(); }
			catch(Exception exAes)
			{
				throw new SecurityException("AES/Rijndael: " + exAes.Message);
			}

			try { new SHA256Managed(); }
			catch(Exception exSha256)
			{
				throw new SecurityException("SHA-256: " + exSha256.Message);
			}
#endif
		}

		private static void TestRijndael()
		{
#if !KeePassRT
			// Test vector (official ECB test vector #356)
			byte[] pbIV = new byte[16];
			byte[] pbTestKey = new byte[32];
			byte[] pbTestData = new byte[16];
			byte[] pbReferenceCT = new byte[16] {
				0x75, 0xD1, 0x1B, 0x0E, 0x3A, 0x68, 0xC4, 0x22,
				0x3D, 0x88, 0xDB, 0xF0, 0x17, 0x97, 0x7D, 0xD7 };
			int i;

			for(i = 0; i < 16; ++i) pbIV[i] = 0;
			for(i = 0; i < 32; ++i) pbTestKey[i] = 0;
			for(i = 0; i < 16; ++i) pbTestData[i] = 0;
			pbTestData[0] = 0x04;

			RijndaelManaged r = new RijndaelManaged();

			if(r.BlockSize != 128) // AES block size
			{
				Debug.Assert(false);
				r.BlockSize = 128;
			}

			r.IV = pbIV;
			r.KeySize = 256;
			r.Key = pbTestKey;
			r.Mode = CipherMode.ECB;
			ICryptoTransform iCrypt = r.CreateEncryptor();

			iCrypt.TransformBlock(pbTestData, 0, 16, pbTestData, 0);

			if(!MemUtil.ArraysEqual(pbTestData, pbReferenceCT))
				throw new SecurityException(KLRes.EncAlgorithmAes + ".");
#endif
		}

		private static void TestSalsa20()
		{
			// Test values from official set 6, vector 3
			byte[] pbKey= new byte[32] {
				0x0F, 0x62, 0xB5, 0x08, 0x5B, 0xAE, 0x01, 0x54,
				0xA7, 0xFA, 0x4D, 0xA0, 0xF3, 0x46, 0x99, 0xEC,
				0x3F, 0x92, 0xE5, 0x38, 0x8B, 0xDE, 0x31, 0x84,
				0xD7, 0x2A, 0x7D, 0xD0, 0x23, 0x76, 0xC9, 0x1C
			};
			byte[] pbIV = new byte[8] { 0x28, 0x8F, 0xF6, 0x5D,
				0xC4, 0x2B, 0x92, 0xF9 };
			byte[] pbExpected = new byte[16] {
				0x5E, 0x5E, 0x71, 0xF9, 0x01, 0x99, 0x34, 0x03,
				0x04, 0xAB, 0xB2, 0x2A, 0x37, 0xB6, 0x62, 0x5B
			};

			byte[] pb = new byte[16];
			Salsa20Cipher c = new Salsa20Cipher(pbKey, pbIV);
			c.Encrypt(pb, pb.Length, false);
			if(!MemUtil.ArraysEqual(pb, pbExpected))
				throw new SecurityException("Salsa20.");

#if DEBUG
			// Extended test in debug mode
			byte[] pbExpected2 = new byte[16] {
				0xAB, 0xF3, 0x9A, 0x21, 0x0E, 0xEE, 0x89, 0x59,
				0x8B, 0x71, 0x33, 0x37, 0x70, 0x56, 0xC2, 0xFE
			};
			byte[] pbExpected3 = new byte[16] {
				0x1B, 0xA8, 0x9D, 0xBD, 0x3F, 0x98, 0x83, 0x97,
				0x28, 0xF5, 0x67, 0x91, 0xD5, 0xB7, 0xCE, 0x23
			};

			Random r = new Random();
			int nPos = Salsa20ToPos(c, r, pb.Length, 65536);
			c.Encrypt(pb, pb.Length, false);
			if(!MemUtil.ArraysEqual(pb, pbExpected2))
				throw new SecurityException("Salsa20-2.");

			nPos = Salsa20ToPos(c, r, nPos + pb.Length, 131008);
			Array.Clear(pb, 0, pb.Length);
			c.Encrypt(pb, pb.Length, true);
			if(!MemUtil.ArraysEqual(pb, pbExpected3))
				throw new SecurityException("Salsa20-3.");
#endif
		}

#if DEBUG
		private static int Salsa20ToPos(Salsa20Cipher c, Random r, int nPos,
			int nTargetPos)
		{
			byte[] pb = new byte[512];

			while(nPos < nTargetPos)
			{
				int x = r.Next(1, 513);
				int nGen = Math.Min(nTargetPos - nPos, x);
				c.Encrypt(pb, nGen, r.Next(0, 2) == 0);
				nPos += nGen;
			}

			return nTargetPos;
		}
#endif

		private static void TestNativeKeyTransform()
		{
#if DEBUG
			byte[] pbOrgKey = CryptoRandom.Instance.GetRandomBytes(32);
			byte[] pbSeed = CryptoRandom.Instance.GetRandomBytes(32);
			ulong uRounds = (ulong)((new Random()).Next(1, 0x3FFF));

			byte[] pbManaged = new byte[32];
			Array.Copy(pbOrgKey, pbManaged, 32);
			if(CompositeKey.TransformKeyManaged(pbManaged, pbSeed, uRounds) == false)
				throw new SecurityException("Managed transform.");

			byte[] pbNative = new byte[32];
			Array.Copy(pbOrgKey, pbNative, 32);
			if(NativeLib.TransformKey256(pbNative, pbSeed, uRounds) == false)
				return; // Native library not available ("success")

			if(!MemUtil.ArraysEqual(pbManaged, pbNative))
				throw new SecurityException("Native transform.");
#endif
		}

		private static void TestMemUtil()
		{
#if DEBUG
			Random r = new Random();
			byte[] pb = CryptoRandom.Instance.GetRandomBytes((uint)r.Next(
				0, 0x2FFFF));

			byte[] pbCompressed = MemUtil.Compress(pb);
			if(!MemUtil.ArraysEqual(MemUtil.Decompress(pbCompressed), pb))
				throw new InvalidOperationException("GZip");

			Encoding enc = StrUtil.Utf8;
			pb = enc.GetBytes("012345678901234567890a");
			byte[] pbN = enc.GetBytes("9012");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 9)
				throw new InvalidOperationException("MemUtil-1");
			pbN = enc.GetBytes("01234567890123");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 0)
				throw new InvalidOperationException("MemUtil-2");
			pbN = enc.GetBytes("a");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 21)
				throw new InvalidOperationException("MemUtil-3");
			pbN = enc.GetBytes("0a");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 20)
				throw new InvalidOperationException("MemUtil-4");
			pbN = enc.GetBytes("1");
			if(MemUtil.IndexOf<byte>(pb, pbN) != 1)
				throw new InvalidOperationException("MemUtil-5");
			pbN = enc.GetBytes("b");
			if(MemUtil.IndexOf<byte>(pb, pbN) >= 0)
				throw new InvalidOperationException("MemUtil-6");
			pbN = enc.GetBytes("012b");
			if(MemUtil.IndexOf<byte>(pb, pbN) >= 0)
				throw new InvalidOperationException("MemUtil-7");
#endif
		}

		private static void TestHmacOtp()
		{
#if (DEBUG && !KeePassLibSD && !KeePassRT)
			byte[] pbSecret = StrUtil.Utf8.GetBytes("12345678901234567890");
			string[] vExp = new string[]{ "755224", "287082", "359152",
				"969429", "338314", "254676", "287922", "162583", "399871",
				"520489" };

			for(int i = 0; i < vExp.Length; ++i)
			{
				if(HmacOtp.Generate(pbSecret, (ulong)i, 6, false, -1) != vExp[i])
					throw new InvalidOperationException("HmacOtp");
			}
#endif
		}

		private static void TestProtectedObjects()
		{
#if DEBUG
			Encoding enc = StrUtil.Utf8;

			byte[] pbData = enc.GetBytes("Test Test Test Test");
			ProtectedBinary pb = new ProtectedBinary(true, pbData);
			if(!pb.IsProtected) throw new SecurityException("ProtectedBinary-1");

			byte[] pbDec = pb.ReadData();
			if(!MemUtil.ArraysEqual(pbData, pbDec))
				throw new SecurityException("ProtectedBinary-2");
			if(!pb.IsProtected) throw new SecurityException("ProtectedBinary-3");

			byte[] pbData2 = enc.GetBytes("Test Test Test Test");
			byte[] pbData3 = enc.GetBytes("Test Test Test Test Test");
			ProtectedBinary pb2 = new ProtectedBinary(true, pbData2);
			ProtectedBinary pb3 = new ProtectedBinary(true, pbData3);
			if(!pb.Equals(pb2)) throw new SecurityException("ProtectedBinary-4");
			if(pb.Equals(pb3)) throw new SecurityException("ProtectedBinary-5");
			if(pb2.Equals(pb3)) throw new SecurityException("ProtectedBinary-6");

			if(pb.GetHashCode() != pb2.GetHashCode())
				throw new SecurityException("ProtectedBinary-7");
			if(!((object)pb).Equals((object)pb2))
				throw new SecurityException("ProtectedBinary-8");
			if(((object)pb).Equals((object)pb3))
				throw new SecurityException("ProtectedBinary-9");
			if(((object)pb2).Equals((object)pb3))
				throw new SecurityException("ProtectedBinary-10");

			ProtectedString ps = new ProtectedString();
			if(ps.Length != 0) throw new SecurityException("ProtectedString-1");
			if(!ps.IsEmpty) throw new SecurityException("ProtectedString-2");
			if(ps.ReadString().Length != 0)
				throw new SecurityException("ProtectedString-3");

			ps = new ProtectedString(true, "Test");
			ProtectedString ps2 = new ProtectedString(true, enc.GetBytes("Test"));
			if(ps.IsEmpty) throw new SecurityException("ProtectedString-4");
			pbData = ps.ReadUtf8();
			pbData2 = ps2.ReadUtf8();
			if(!MemUtil.ArraysEqual(pbData, pbData2))
				throw new SecurityException("ProtectedString-5");
			if(pbData.Length != 4)
				throw new SecurityException("ProtectedString-6");
			if(ps.ReadString() != ps2.ReadString())
				throw new SecurityException("ProtectedString-7");
			pbData = ps.ReadUtf8();
			pbData2 = ps2.ReadUtf8();
			if(!MemUtil.ArraysEqual(pbData, pbData2))
				throw new SecurityException("ProtectedString-8");
			if(!ps.IsProtected) throw new SecurityException("ProtectedString-9");
			if(!ps2.IsProtected) throw new SecurityException("ProtectedString-10");
#endif
		}

		private static void TestStrUtil()
		{
#if DEBUG
			string[] vSeps = new string[]{ "ax", "b", "c" };
			const string str1 = "axbqrstcdeax";
			List<string> v1 = StrUtil.SplitWithSep(str1, vSeps, true);

			if(v1.Count != 9) throw new InvalidOperationException("StrUtil-1");
			if(v1[0].Length > 0) throw new InvalidOperationException("StrUtil-2");
			if(!v1[1].Equals("ax")) throw new InvalidOperationException("StrUtil-3");
			if(v1[2].Length > 0) throw new InvalidOperationException("StrUtil-4");
			if(!v1[3].Equals("b")) throw new InvalidOperationException("StrUtil-5");
			if(!v1[4].Equals("qrst")) throw new InvalidOperationException("StrUtil-6");
			if(!v1[5].Equals("c")) throw new InvalidOperationException("StrUtil-7");
			if(!v1[6].Equals("de")) throw new InvalidOperationException("StrUtil-8");
			if(!v1[7].Equals("ax")) throw new InvalidOperationException("StrUtil-9");
			if(v1[8].Length > 0) throw new InvalidOperationException("StrUtil-10");

			const string str2 = "12ab56";
			List<string> v2 = StrUtil.SplitWithSep(str2, new string[]{ "AB" }, false);
			if(v2.Count != 3) throw new InvalidOperationException("StrUtil-11");
			if(!v2[0].Equals("12")) throw new InvalidOperationException("StrUtil-12");
			if(!v2[1].Equals("AB")) throw new InvalidOperationException("StrUtil-13");
			if(!v2[2].Equals("56")) throw new InvalidOperationException("StrUtil-14");

			List<string> v3 = StrUtil.SplitWithSep("pqrs", vSeps, false);
			if(v3.Count != 1) throw new InvalidOperationException("StrUtil-15");
			if(!v3[0].Equals("pqrs")) throw new InvalidOperationException("StrUtil-16");

			if(StrUtil.VersionToString(0x000F000E000D000CUL) != "15.14.13.12")
				throw new InvalidOperationException("StrUtil-V1");
			if(StrUtil.VersionToString(0x00FF000E00010000UL) != "255.14.1")
				throw new InvalidOperationException("StrUtil-V2");
			if(StrUtil.VersionToString(0x000F00FF00000000UL) != "15.255")
				throw new InvalidOperationException("StrUtil-V3");
			if(StrUtil.VersionToString(0x00FF000000000000UL) != "255")
				throw new InvalidOperationException("StrUtil-V4");
			if(StrUtil.VersionToString(0x00FF000000000000UL, true) != "255.0")
				throw new InvalidOperationException("StrUtil-V5");
			if(StrUtil.VersionToString(0x0000000000070000UL, true) != "0.0.7")
				throw new InvalidOperationException("StrUtil-V6");

			if(StrUtil.RtfEncodeChar('\u0000') != "\\u0?")
				throw new InvalidOperationException("StrUtil-Rtf1");
			if(StrUtil.RtfEncodeChar('\u7FFF') != "\\u32767?")
				throw new InvalidOperationException("StrUtil-Rtf2");
			if(StrUtil.RtfEncodeChar('\u8000') != "\\u-32768?")
				throw new InvalidOperationException("StrUtil-Rtf3");
			if(StrUtil.RtfEncodeChar('\uFFFF') != "\\u-1?")
				throw new InvalidOperationException("StrUtil-Rtf4");
#endif
		}

		private static void TestUrlUtil()
		{
#if DEBUG
			if(UrlUtil.GetHost(@"scheme://domain:port/path?query_string#fragment_id") !=
				"domain")
				throw new InvalidOperationException("UrlUtil-H1");
			if(UrlUtil.GetHost(@"http://example.org:80") != "example.org")
				throw new InvalidOperationException("UrlUtil-H2");
			if(UrlUtil.GetHost(@"mailto:bob@example.com") != "example.com")
				throw new InvalidOperationException("UrlUtil-H3");
			if(UrlUtil.GetHost(@"ftp://asmith@ftp.example.org") != "ftp.example.org")
				throw new InvalidOperationException("UrlUtil-H4");
			if(UrlUtil.GetHost(@"scheme://username:password@domain:port/path?query_string#fragment_id") !=
				"domain")
				throw new InvalidOperationException("UrlUtil-H5");
			if(UrlUtil.GetHost(@"bob@example.com") != "example.com")
				throw new InvalidOperationException("UrlUtil-H6");
			if(UrlUtil.GetHost(@"s://u:p@d.tld:p/p?q#f") != "d.tld")
				throw new InvalidOperationException("UrlUtil-H7");

			if(NativeLib.IsUnix()) return;

			string strBase = "\\\\HOMESERVER\\Apps\\KeePass\\KeePass.exe";
			string strDoc = "\\\\HOMESERVER\\Documents\\KeePass\\NewDatabase.kdbx";
			string strRel = "..\\..\\Documents\\KeePass\\NewDatabase.kdbx";

			string str = UrlUtil.MakeRelativePath(strBase, strDoc);
			if(!str.Equals(strRel)) throw new InvalidOperationException("UrlUtil-R1");

			str = UrlUtil.MakeAbsolutePath(strBase, strRel);
			if(!str.Equals(strDoc)) throw new InvalidOperationException("UrlUtil-R2");
#endif
		}
	}
}
