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
using System.Xml;
using System.IO;
using System.Drawing;
using System.Diagnostics;

using KeePass.Resources;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Interfaces;
using KeePassLib.Security;

namespace KeePass.DataExchange.Formats
{
	// 2.50-2.75+
	internal sealed class AmpXml250 : FileFormatProvider
	{
		private const string ElemRoot = "AmP_FILE";

		private const string ElemInfo = "INFO";
		private const string ElemData = "DATA";

		private const string ElemCategory = "Kategorie";
		private const string ElemTitle = "Bezeichnung";
		private const string ElemUserName = "Benutzername";
		private const string ElemPassword1 = "Passwort1";
		private const string ElemPassword2 = "Passwort2";
		private const string ElemExpiry = "Ablaufdatum";
		private const string ElemUrl = "URL_Programm";
		private const string ElemNotes = "Kommentar";

		public override bool SupportsImport { get { return true; } }
		public override bool SupportsExport { get { return false; } }

		public override string FormatName { get { return "Alle meine Passworte XML"; } }
		public override string DefaultExtension { get { return "xml"; } }
		public override string ApplicationGroup { get { return KPRes.PasswordManagers; } }

		public override Image SmallIcon
		{
			get { return KeePass.Properties.Resources.B16x16_Imp_AmP; }
		}

		public override void Import(PwDatabase pwStorage, Stream sInput,
			IStatusLogger slLogger)
		{
			StreamReader sr = new StreamReader(sInput, Encoding.Default);
			string strDoc = sr.ReadToEnd();
			sr.Close();

			strDoc = XmlUtil.DecodeNonStandardEntities(strDoc);

			ImportFileString(strDoc, pwStorage, slLogger);
		}

		private static void ImportFileString(string strXmlDoc, PwDatabase pwStorage,
			IStatusLogger slLogger)
		{
			XmlDocument doc = new XmlDocument();
			doc.LoadXml(strXmlDoc);

			XmlElement xmlRoot = doc.DocumentElement;
			Debug.Assert(xmlRoot.Name == ElemRoot);

			foreach(XmlNode xmlChild in xmlRoot.ChildNodes)
			{
				if(xmlChild.Name == ElemData)
					LoadDataNode(xmlChild, pwStorage, slLogger);
				else if(xmlChild.Name == ElemInfo) { }
				else { Debug.Assert(false); }
			}
		}

		private static void LoadDataNode(XmlNode xmlNode, PwDatabase pwStorage,
			IStatusLogger slLogger)
		{
			uint uCat = 0, uCount = (uint)xmlNode.ChildNodes.Count;
			foreach(XmlNode xmlCategory in xmlNode.ChildNodes)
			{
				LoadCategoryNode(xmlCategory, pwStorage);
				++uCat;
				ImportUtil.SetStatus(slLogger, (uCat * 100) / uCount);
			}
		}

		private static void LoadCategoryNode(XmlNode xmlNode, PwDatabase pwStorage)
		{
			PwGroup pg = new PwGroup(true, true, xmlNode.Name, PwIcon.Folder);
			pwStorage.RootGroup.AddGroup(pg, true);

			PwEntry pe = new PwEntry(true, true);

			foreach(XmlNode xmlChild in xmlNode)
			{
				string strInner = xmlChild.InnerText;
				if(strInner == @"n/a") strInner = string.Empty;

				if(xmlChild.Name == ElemCategory)
				{
					Debug.Assert(strInner == pg.Name);
				}
				else if(xmlChild.Name == ElemTitle)
				{
					AddEntryIfValid(pg, pe);

					pe = new PwEntry(true, true);

					pe.Strings.Set(PwDefs.TitleField, new ProtectedString(
						pwStorage.MemoryProtection.ProtectTitle, strInner));
				}
				else if(xmlChild.Name == ElemUserName)
					pe.Strings.Set(PwDefs.UserNameField, new ProtectedString(
						pwStorage.MemoryProtection.ProtectUserName, strInner));
				else if(xmlChild.Name == ElemPassword1)
					pe.Strings.Set(PwDefs.PasswordField, new ProtectedString(
						pwStorage.MemoryProtection.ProtectPassword, strInner));
				else if(xmlChild.Name == ElemPassword2)
					pe.Strings.Set(PwDefs.PasswordField + @" 2", new ProtectedString(
						pwStorage.MemoryProtection.ProtectPassword, strInner));
				else if(xmlChild.Name == ElemExpiry)
				{
					try
					{
						DateTime dt = DateTime.Parse(strInner);
						pe.ExpiryTime = dt;
						pe.Expires = true;
					}
					catch(Exception) { }
				}
				else if(xmlChild.Name == ElemUrl)
					pe.Strings.Set(PwDefs.UrlField, new ProtectedString(
						pwStorage.MemoryProtection.ProtectUrl, strInner));
				else if(xmlChild.Name == ElemNotes)
					pe.Strings.Set(PwDefs.NotesField, new ProtectedString(
						pwStorage.MemoryProtection.ProtectNotes, strInner));
				else { Debug.Assert(false); }
			}

			AddEntryIfValid(pg, pe);
		}

		private static void AddEntryIfValid(PwGroup pgContainer, PwEntry pe)
		{
			if(pe == null) return;

			if((pe.Strings.ReadSafe(PwDefs.TitleField).Length == 0) &&
				(pe.Strings.ReadSafe(PwDefs.UserNameField).Length == 0))
			{
				return;
			}

			pgContainer.AddEntry(pe, true);
		}
	}
}
