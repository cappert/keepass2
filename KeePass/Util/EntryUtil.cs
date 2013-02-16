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
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

using KeePass.Forms;
using KeePass.Resources;
using KeePass.UI;
using KeePass.Util.Spr;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Delegates;
using KeePassLib.Security;
using KeePassLib.Utility;
using KeePassLib.Serialization;

namespace KeePass.Util
{
	/// <summary>
	/// This class contains various static functions for entry operations.
	/// </summary>
	public static class EntryUtil
	{
		/// <summary>
		/// Save all attachments of an array of entries to a directory.
		/// </summary>
		/// <param name="vEntries">Array of entries whose attachments are extracted and saved.</param>
		/// <param name="strBasePath">Directory in which the attachments are stored.</param>
		public static void SaveEntryAttachments(PwEntry[] vEntries, string strBasePath)
		{
			Debug.Assert(vEntries != null); if(vEntries == null) return;
			Debug.Assert(strBasePath != null); if(strBasePath == null) return;

			string strPath = UrlUtil.EnsureTerminatingSeparator(strBasePath, false);
			bool bCancel = false;

			foreach(PwEntry pe in vEntries)
			{
				foreach(KeyValuePair<string, ProtectedBinary> kvp in pe.Binaries)
				{
					string strFile = strPath + kvp.Key;

					if(File.Exists(strFile))
					{
						string strMsg = KPRes.FileExistsAlready + MessageService.NewLine;
						strMsg += strFile + MessageService.NewParagraph;
						strMsg += KPRes.OverwriteExistingFileQuestion;

						DialogResult dr = MessageService.Ask(strMsg, null,
							MessageBoxButtons.YesNoCancel);

						if(dr == DialogResult.Cancel)
						{
							bCancel = true;
							break;
						}
						else if(dr == DialogResult.Yes)
						{
							try { File.Delete(strFile); }
							catch(Exception exDel)
							{
								MessageService.ShowWarning(strFile, exDel);
								continue;
							}
						}
						else continue; // DialogResult.No
					}

					byte[] pbData = kvp.Value.ReadData();
					try { File.WriteAllBytes(strFile, pbData); }
					catch(Exception exWrite)
					{
						MessageService.ShowWarning(strFile, exWrite);
					}
					MemUtil.ZeroByteArray(pbData);
				}
				if(bCancel) break;
			}
		}

		// Old format name (<= 2.14): "KeePassEntriesCF"
		public const string ClipFormatEntries = "KeePassEntriesCX";
		private static byte[] AdditionalEntropy = { 0xF8, 0x03, 0xFA, 0x51, 0x87, 0x18, 0x49, 0x5D };

		[Obsolete]
		public static void CopyEntriesToClipboard(PwDatabase pwDatabase, PwEntry[] vEntries)
		{
			CopyEntriesToClipboard(pwDatabase, vEntries, IntPtr.Zero);
		}

		public static void CopyEntriesToClipboard(PwDatabase pwDatabase, PwEntry[] vEntries,
			IntPtr hOwner)
		{
			MemoryStream ms = new MemoryStream();
			GZipStream gz = new GZipStream(ms, CompressionMode.Compress);
			KdbxFile.WriteEntries(gz, vEntries);

			byte[] pbFinal;
			if(WinUtil.IsWindows9x) pbFinal = ms.ToArray();
			else pbFinal = ProtectedData.Protect(ms.ToArray(), AdditionalEntropy,
				DataProtectionScope.CurrentUser);

			ClipboardUtil.Copy(pbFinal, ClipFormatEntries, true, true, hOwner);

			gz.Close(); ms.Close();
		}

		public static void PasteEntriesFromClipboard(PwDatabase pwDatabase,
			PwGroup pgStorage)
		{
			try { PasteEntriesFromClipboardPriv(pwDatabase, pgStorage); }
			catch(Exception) { Debug.Assert(false); }
		}

		private static void PasteEntriesFromClipboardPriv(PwDatabase pwDatabase,
			PwGroup pgStorage)
		{
			if(!Clipboard.ContainsData(ClipFormatEntries)) return;

			byte[] pbEnc = ClipboardUtil.GetEncodedData(ClipFormatEntries, IntPtr.Zero);
			if(pbEnc == null) { Debug.Assert(false); return; }

			byte[] pbPlain;
			if(WinUtil.IsWindows9x) pbPlain = pbEnc;
			else pbPlain = ProtectedData.Unprotect(pbEnc, AdditionalEntropy,
				DataProtectionScope.CurrentUser);

			MemoryStream ms = new MemoryStream(pbPlain, false);
			GZipStream gz = new GZipStream(ms, CompressionMode.Decompress);

			List<PwEntry> vEntries = KdbxFile.ReadEntries(gz);

			// Adjust protection settings and add entries
			foreach(PwEntry pe in vEntries)
			{
				pe.Strings.EnableProtection(PwDefs.TitleField,
					pwDatabase.MemoryProtection.ProtectTitle);
				pe.Strings.EnableProtection(PwDefs.UserNameField,
					pwDatabase.MemoryProtection.ProtectUserName);
				pe.Strings.EnableProtection(PwDefs.PasswordField,
					pwDatabase.MemoryProtection.ProtectPassword);
				pe.Strings.EnableProtection(PwDefs.UrlField,
					pwDatabase.MemoryProtection.ProtectUrl);
				pe.Strings.EnableProtection(PwDefs.NotesField,
					pwDatabase.MemoryProtection.ProtectNotes);

				pgStorage.AddEntry(pe, true, true);
			}

			gz.Close(); ms.Close();
		}

		public static string FillPlaceholders(string strText, SprContext ctx)
		{
			if((ctx == null) || (ctx.Entry == null)) return strText;

			string str = strText;

			if((ctx.Flags & SprCompileFlags.NewPassword) != SprCompileFlags.None)
				str = ReplaceNewPasswordPlaceholder(str, ctx);

			if((ctx.Flags & SprCompileFlags.HmacOtp) != SprCompileFlags.None)
				str = ReplaceHmacOtpPlaceholder(str, ctx);

			return str;
		}

		private static string ReplaceNewPasswordPlaceholder(string strText,
			SprContext ctx)
		{
			PwEntry pe = ctx.Entry;
			PwDatabase pd = ctx.Database;
			if((pe == null) || (pd == null)) return strText;

			string str = strText;

			const string strNewPwPlh = @"{NEWPASSWORD}";
			if(str.IndexOf(strNewPwPlh, StrUtil.CaseIgnoreCmp) >= 0)
			{
				ProtectedString psAutoGen;
				PwgError e = PwGenerator.Generate(out psAutoGen,
					Program.Config.PasswordGenerator.AutoGeneratedPasswordsProfile,
					null, Program.PwGeneratorPool);
				psAutoGen = psAutoGen.WithProtection(pd.MemoryProtection.ProtectPassword);

				if(e == PwgError.Success)
				{
					pe.CreateBackup(pd);
					pe.Strings.Set(PwDefs.PasswordField, psAutoGen);
					pe.Touch(true, false);
					pd.Modified = true;

					string strIns = SprEngine.TransformContent(psAutoGen.ReadString(), ctx);
					str = StrUtil.ReplaceCaseInsensitive(str, strNewPwPlh, strIns);
				}
			}

			return str;
		}

		private static string ReplaceHmacOtpPlaceholder(string strText,
			SprContext ctx)
		{
			PwEntry pe = ctx.Entry;
			PwDatabase pd = ctx.Database;
			if((pe == null) || (pd == null)) return strText;

			string str = strText;

			const string strHmacOtpPlh = @"{HMACOTP}";
			if(str.IndexOf(strHmacOtpPlh, StrUtil.CaseIgnoreCmp) >= 0)
			{
				const string strKeyField = "HmacOtp-Secret";
				const string strCounterField = "HmacOtp-Counter";

				byte[] pbSecret = StrUtil.Utf8.GetBytes(pe.Strings.ReadSafe(
					strKeyField));

				string strCounter = pe.Strings.ReadSafe(strCounterField);
				ulong uCounter;
				ulong.TryParse(strCounter, out uCounter);

				string strValue = HmacOtp.Generate(pbSecret, uCounter, 6, false, -1);

				pe.Strings.Set(strCounterField, new ProtectedString(false,
					(uCounter + 1).ToString()));
				pd.Modified = true;

				str = StrUtil.ReplaceCaseInsensitive(str, strHmacOtpPlh, strValue);
			}

			return str;
		}

		public static bool EntriesHaveSameParent(PwObjectList<PwEntry> v)
		{
			if(v == null) { Debug.Assert(false); return true; }
			if(v.UCount == 0) return true;

			PwGroup pg = v.GetAt(0).ParentGroup;
			foreach(PwEntry pe in v)
			{
				if(pe.ParentGroup != pg) return false;
			}

			return true;
		}

		public static void ReorderEntriesAsInDatabase(PwObjectList<PwEntry> v,
			PwDatabase pd)
		{
			if((v == null) || (pd == null)) { Debug.Assert(false); return; }

			PwObjectList<PwEntry> vRem = v.CloneShallow();
			v.Clear();

			EntryHandler eh = delegate(PwEntry pe)
			{
				int p = vRem.IndexOf(pe);
				if(p >= 0)
				{
					v.Add(pe);
					vRem.RemoveAt((uint)p);
				}

				return true;
			};

			pd.RootGroup.TraverseTree(TraversalMethod.PreOrder, null, eh);

			foreach(PwEntry peRem in vRem) v.Add(peRem); // Entries not found
		}

		[Obsolete]
		public static void ExpireTanEntry(PwEntry pe)
		{
			ExpireTanEntryIfOption(pe, null);
		}

		[Obsolete]
		public static bool ExpireTanEntryIfOption(PwEntry pe)
		{
			return ExpireTanEntryIfOption(pe, null);
		}

		/// <summary>
		/// Test whether an entry is a TAN entry and if so, expire it, provided
		/// that the option for expiring TANs on use is enabled.
		/// </summary>
		/// <param name="pe">Entry.</param>
		/// <returns>If the entry has been modified, the return value is
		/// <c>true</c>, otherwise <c>false</c>.</returns>
		public static bool ExpireTanEntryIfOption(PwEntry pe, PwDatabase pdContext)
		{
			if(pe == null) throw new ArgumentNullException("pe");
			// pdContext may be null
			if(!PwDefs.IsTanEntry(pe)) return false; // No assert

			if(Program.Config.Defaults.TanExpiresOnUse)
			{
				pe.ExpiryTime = DateTime.Now;
				pe.Expires = true;
				pe.Touch(true);
				if(pdContext != null) pdContext.Modified = true;
				return true;
			}

			return false;
		}

		public static string CreateSummaryList(PwGroup pgItems, bool bStartWithNewPar)
		{
			List<PwEntry> l = pgItems.GetEntries(true).CloneShallowToList();
			string str = CreateSummaryList(pgItems, l.ToArray());

			if((str.Length == 0) || !bStartWithNewPar) return str;
			return (MessageService.NewParagraph + str);
		}

		public static string CreateSummaryList(PwGroup pgSubGroups, PwEntry[] vEntries)
		{
			int nMaxEntries = 10;
			string strSummary = string.Empty;

			if(pgSubGroups != null)
			{
				PwObjectList<PwGroup> vGroups = pgSubGroups.GetGroups(true);
				if(vGroups.UCount > 0)
				{
					StringBuilder sbGroups = new StringBuilder();
					sbGroups.Append("- ");
					uint uToList = Math.Min(3U, vGroups.UCount);
					for(uint u = 0; u < uToList; ++u)
					{
						if(sbGroups.Length > 2) sbGroups.Append(", ");
						sbGroups.Append(vGroups.GetAt(u).Name);
					}
					if(uToList < vGroups.UCount) sbGroups.Append(", ...");
					strSummary += sbGroups.ToString(); // New line below

					nMaxEntries -= 2;
				}
			}

			int nSummaryShow = Math.Min(nMaxEntries, vEntries.Length);
			if(nSummaryShow == (vEntries.Length - 1)) --nSummaryShow; // Plural msg

			for(int iSumEnum = 0; iSumEnum < nSummaryShow; ++iSumEnum)
			{
				if(strSummary.Length > 0) strSummary += MessageService.NewLine;

				PwEntry pe = vEntries[iSumEnum];
				strSummary += ("- " + StrUtil.CompactString3Dots(
					pe.Strings.ReadSafe(PwDefs.TitleField), 39));
				if(PwDefs.IsTanEntry(pe))
				{
					string strTanIdx = pe.Strings.ReadSafe(PwDefs.UserNameField);
					if(!string.IsNullOrEmpty(strTanIdx))
						strSummary += (@" (#" + strTanIdx + @")");
				}
			}
			if(nSummaryShow != vEntries.Length)
				strSummary += (MessageService.NewLine + "- " +
					KPRes.MoreEntries.Replace(@"{PARAM}", (vEntries.Length -
					nSummaryShow).ToString()));

			return strSummary;
		}
	}
}
