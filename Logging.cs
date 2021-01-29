using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Logging {
	public enum eLogType {
		Error = 1,
		Warning = 2
	}

	/// <summary>
	/// Contenitore per tutte le informazioni che vengono inserite per ogni log
	/// </summary>
	public struct LogData {
		/// <summary>
		/// Data di generazione del log
		/// </summary>
		public DateTime Date { get; set; }
		/// <summary>
		/// Tipo del log specificato
		/// </summary>
		public eLogType Type { get; set; }
		/// <summary>
		/// Messaggio inserito o generato dall'eccezione
		/// </summary>
		public string Message { get; set; }
		public string Source { get; set; }
		public string StackTrace { get; set; }
		public string TargetSite { get; set; }
	}

	/// <summary>
	/// Classe per scrivere e leggere log in formato JSON
	/// </summary>
	public class Logger {
		/// <summary>
		/// Il template del file log creato (la stringa <c>yyyyMMdd</c> viene automaticamente sostituita con il valore di <c>DateTime.Now</c>)
		/// <para>Esempio: Log_yyyyMMdd.txt  -->  Log_19000101.txt</para>
		/// </summary>
		public static string FileNameTemplate { get; set; } = "Log_yyyyMMdd.txt";

		/// <summary>
		/// Funzione chiamata alla creazione del nome del file di testo da creare/cercare.
		/// <para>NULL di default</para>
		/// </summary>
		public static Func<string> FileNameCreator { get; set; } = null;

		/// <summary>
		/// La directory dove verranno salvati e letti i file di log generati.
		/// <para>C:\Logger\Log di default</para>
		/// </summary>
		public static string TargetDirectoryPath { get; set; } = "C:/Logger/Log/";

		/// <summary>
		/// Imposta se la formattazione dell'oggetto JSON scritto su file deve essere indentata o no
		/// <para>TRUE di default</para>
		/// </summary>
		/// <param name="Readable"></param>
		public static bool FormattingReadable {
			get {
				return MainWriter.JsonFormatting == Formatting.Indented && SecondWriter.JsonFormatting == Formatting.Indented;
			}

			set {
				MainWriter.JsonFormatting = value ? Formatting.Indented : Formatting.None;
				SecondWriter.JsonFormatting = value ? Formatting.Indented : Formatting.None;
			}
		}

		/// <summary>
		/// Imposta se i messaggi scritti da log devo avere un struttura più verbosa. I caso di oggetti Exception vengono inserite anche tutti i messaggi di tutte le InnerException presenti.
		/// <para>FALSE di default</para>
		/// </summary>
		public static bool Verbose { get; set; } = false;


		private static WriterThread MainWriter { get; set; } = new WriterThread("MainWriter");
		private static WriterThread SecondWriter { get; set; } = new WriterThread("SecondWriter");



		/// <summary>
		/// Crea e scrive su un file di testo le informazioni dell'eccezione passata. 
		/// Il file se creato presenta come nome quello presente nella FileNameTemplate oppure quello generato dalla FileNameCreator.
		/// </summary>
		public static void Write<T>(eLogType MessageType, T Ex) where T : Exception {
			if (Ex is null) {
				throw new ArgumentNullException("Ex");
			}

			try {
				string strMessage = CreateObjectMessage(Ex, Logger.Verbose);

				//
				// Creo il mio oggetto da scrivere
				//
				LogData obj = new LogData {
					Date = DateTime.Now,
					Type = MessageType,
					Message = strMessage,
					Source = Ex.Source,
					StackTrace = Ex.StackTrace,
					TargetSite = Ex.TargetSite?.ToString()
				};

				QueueAndWrite(obj);
			}
			catch (Exception ex) {
				Console.WriteLine("Logger.Write -> ", ex.Message);
			}
		}

		/// <summary>
		/// Crea e scrive su un file di testo le informazioni dell'eccezione passata. 
		/// Il file se creato presenta come nome quello presente nella FileNameTemplate oppure quello generato dalla FileNameCreator.
		/// </summary>
		public static void Write<T>(eLogType MessageType, T Ex, bool Verbose) where T : Exception {
			if (Ex is null) {
				throw new ArgumentNullException("Ex");
			}

			try {
				string strMessage = CreateObjectMessage(Ex, Verbose);

				//
				// Creo il mio oggetto da scrivere
				//
				LogData obj = new LogData {
					Date = DateTime.Now,
					Type = MessageType,
					Message = strMessage,
					Source = Ex.Source,
					StackTrace = Ex.StackTrace,
					TargetSite = Ex.TargetSite?.ToString()
				};

				QueueAndWrite(obj);
			}
			catch (Exception ex) {
				Console.WriteLine("Logger.Write -> ", ex.Message);
			}
		}

		/// <summary>
		/// Crea e scrive su un file di testo il messaggio passato come parametro e del tipo specificato. 
		/// Il file se creato presenta come nome quello presente nella FileNameTemplate oppure quello generato dalla FileNameCreator.
		/// </summary>
		public static void Write(eLogType MessageType, string Message) {
			if (Message is null) {
				throw new ArgumentNullException("Message");
			}

			try {
				//
				// Creo il mio oggetto da scrivere
				//
				LogData obj = new LogData {
					Date = DateTime.Now,
					Type = MessageType,
					Message = Message.Trim(),
					Source = "",
					StackTrace = "",
					TargetSite = ""
				};

				QueueAndWrite(obj);
			}
			catch (Exception ex) {
				Console.WriteLine("Logger.Write -> ", ex.Message);
			}
		}



		/// <summary>
		/// Cerca di recuperare tutti i file creati nella data passata come parametro.
		/// Ritorna un Dictionary contente le informazioni sui file trovati e una lista di LogData per ognuno di loro.
		/// </summary>
		/// <param name="FilterDate"></param>
		/// <returns></returns>
		public static Dictionary<FileInfo, List<LogData>> GetLogsByDate(DateTime FilterDate) {
			if (string.IsNullOrEmpty(TargetDirectoryPath)) {
				throw new ArgumentNullException("TargetDirectoryPath cannot be null or empty");
			}

			Dictionary<FileInfo, List<LogData>> lstFileLogs = new Dictionary<FileInfo, List<LogData>>();

			if (Directory.Exists(TargetDirectoryPath)) {
				//
				// Recupero tutti i file con la data passata come parametro
				//
				string[] objFilesPaths = Directory.GetFiles(TargetDirectoryPath);

				if (objFilesPaths.Length > 0) {
					//
					// Ciclo tutti i file e ne recupero i dati JSON
					//
					foreach (string strFilePath in objFilesPaths) {
						DateTime objFileCreationTime = File.GetCreationTime(strFilePath);
						if (FilterDate.Date.Equals(objFileCreationTime.Date)) {
							string strText = File.ReadAllText(strFilePath);

							List<LogData> objLogsInformations = JsonConvert.DeserializeObject<List<LogData>>(strText);

							lstFileLogs.Add(new FileInfo(strFilePath), objLogsInformations);
						}
					}
				}
			}

			return lstFileLogs;
		}

		/// <summary>
		/// Cerca di recuperare tutti i file creati tra le due date (comprese) passate come parametro.
		/// Ritorna un Dictionary contente le informazioni sui file trovati e una lista di LogData per ognuno di loro
		/// </summary>
		/// <param name="MinDate"></param>
		/// <param name="MaxDate"></param>
		/// <returns></returns>
		public static Dictionary<FileInfo, List<LogData>> GetLogsByDateRange(DateTime MinDate, DateTime MaxDate) {
			if (string.IsNullOrEmpty(TargetDirectoryPath)) {
				throw new ArgumentNullException("TargetDirectoryPath cannot be null or empty");
			}

			Dictionary<FileInfo, List<LogData>> lstFileLogs = new Dictionary<FileInfo, List<LogData>>();

			if (Directory.Exists(TargetDirectoryPath)) {
				//
				// Recupero tutti i file con la data compresa tra le date passate come parametro
				// 
				string[] objFilesPaths = Directory.GetFiles(TargetDirectoryPath);

				if (objFilesPaths.Length > 0) {
					//
					// Ciclo tutti i file e ne recupero i dati JSON
					//
					foreach (string strFilePath in objFilesPaths) {
						DateTime objFileCreationTime = File.GetCreationTime(strFilePath);
						if (MinDate.Date <= objFileCreationTime.Date
							&& objFileCreationTime.Date <= MaxDate.Date) {
							string strText = File.ReadAllText(strFilePath);

							List<LogData> objLogsInformations = JsonConvert.DeserializeObject<List<LogData>>(strText);

							lstFileLogs.Add(new FileInfo(strFilePath), objLogsInformations);
						}
					}
				}
			}

			return lstFileLogs;
		}

		/// <summary>
		/// Cerca il file con il nome passato come parametro.
		/// Ritorna un Dictionary contente le informazioni sui file trovati e una lista di LogData per ognuno di loro.
		/// </summary>
		/// <param name="MinDate"></param>
		/// <param name="MaxDate"></param>
		/// <param name="FileName"></param>
		/// <returns></returns>
		public static Dictionary<FileInfo, List<LogData>> GetLogsByName(string FileName) {
			if (string.IsNullOrEmpty(TargetDirectoryPath)) {
				throw new ArgumentNullException("TargetDirectoryPath cannot be null or empty");
			}

			if (string.IsNullOrEmpty(FileName)) {
				throw new ArgumentNullException(nameof(FileName));
			}

			Dictionary<FileInfo, List<LogData>> lstFileLogs = new Dictionary<FileInfo, List<LogData>>();
			FileName = FileName.Trim();

			if (Directory.Exists(TargetDirectoryPath)) {
				//
				// Recupero tutti i file con la data compresa tra le date passate come parametro
				// 
				string[] objFilesPaths = Directory.GetFiles(TargetDirectoryPath);

				if (objFilesPaths.Length > 0) {

					//
					// Ciclo tutti i file e ne recupero i dati JSON
					//
					foreach (string strFilePath in objFilesPaths) {
						string strFileName = Path.GetFileName(strFilePath);
						if (FileName.Equals(strFileName)) {
							string strText = File.ReadAllText(strFilePath);

							List<LogData> objLogsInformations = JsonConvert.DeserializeObject<List<LogData>>(strText);

							lstFileLogs.Add(new FileInfo(strFilePath), objLogsInformations);
						}
					}
				}
			}

			return lstFileLogs;
		}



		private static void QueueAndWrite(LogData JsonObj) {
			try {
				string strFileName;

				if (FileNameCreator == null) {
					strFileName = FileNameTemplate;
					strFileName = strFileName.Replace("yyyyMMdd", DateTime.Now.ToString("yyyyMMdd"));
				}
				else {
					strFileName = FileNameCreator.Invoke();
				}

				WriterThread objWriter = GetWriter(strFileName);

				objWriter.LogQueue.Enqueue(JsonObj);

				//
				// Se il WriterThread non è avviato lo avvio, altrimenti continuo
				//
				if (!objWriter.Running) {
					//
					// Devo impostare qui il Running a TRUE altrimenti c'è il rischio che un richiesta aggiuntiva di scrittura su log arrivi
					// prima che il thread parta causando un errore
					//
					objWriter.Running = true;
					objWriter.FileTarget = strFileName;
					new Thread(new ThreadStart(objWriter.Write)).Start();
				}
			}
			catch (Exception ex) {
				throw new Exception("QueueAndWrite -> " + ex.Message);
			}
		}

		/// <summary>
		/// Restituisce il WriterThread corretto basandosi sul nome del file di destinazione passato e i parametri degli oggetti WriterThread
		/// </summary>
		/// <param name="FileName"></param>
		/// <returns></returns>
		private static WriterThread GetWriter(string FileName) {
			if (string.IsNullOrWhiteSpace(MainWriter.FileTarget)) {
				if (string.IsNullOrWhiteSpace(SecondWriter.FileTarget))
					return MainWriter;
				else
					return SecondWriter;
			}
			else if (MainWriter.FileTarget.Equals(FileName)) {
				return MainWriter;
			}
			else {
				return SecondWriter;
			}
		}

		private static string CreateObjectMessage<T>(T Ex, bool Verbose) where T : Exception {
			string strMessage = "";

			if (Verbose) {
				strMessage += $"Generated exceptions:\n- Exception Name: {Ex.GetType().FullName} " +
								$"\n- Exception Message: {Ex.Message}";

				Exception objInnerEx = Ex;
				while (objInnerEx.InnerException != null) {
					objInnerEx = Ex.InnerException;

					strMessage += $"\n\n- InnerException Name: {objInnerEx.GetType().FullName} " +
									$"\n- InnerException Message: {objInnerEx.Message}";
				}
			}
			else {
				strMessage = Ex.GetType().FullName + " : " + Ex.Message;
			}

			return strMessage;
		}



		private class WriterThread {
			public Formatting JsonFormatting { get; set; }
			public bool Running { get; set; }
			public string FileTarget { get; set; }
			public ConcurrentQueue<LogData> LogQueue { get; }

			private string m_strThreadName;

			public WriterThread(string Name) {
				m_strThreadName = Name;
				LogQueue = new ConcurrentQueue<LogData>();
				JsonFormatting = Formatting.Indented;
			}

			public void Write() {
				Running = true;

				Directory.CreateDirectory(TargetDirectoryPath);
				string strFilePath = Path.Combine(TargetDirectoryPath, FileTarget);
				try {
					//
					// Caricamento nella lista di tutti gli item log
					//
					List<LogData> lstLogs = new List<LogData>();
					while (LogQueue.TryDequeue(out LogData JsonObj)) {
						lstLogs.Add(JsonObj);
					}

					string strTextToAdd = JsonConvert.SerializeObject(lstLogs, JsonFormatting);

					if (File.Exists(strFilePath)) {
						using (FileStream objFileStream = new FileStream(strFilePath, FileMode.Open, FileAccess.ReadWrite)) {

							if (objFileStream.Length > 0) {
								int iCharOffset = 8;

								objFileStream.Seek(-iCharOffset, SeekOrigin.End);

								byte[] array = new byte[iCharOffset];
								int iReadBytes = 0;
								int iBytesOffeset = iCharOffset;

								while (iBytesOffeset > 0) {
									int n = objFileStream.Read(array, iReadBytes, iBytesOffeset);

									if (n == 0)
										break;

									iReadBytes += n;
									iBytesOffeset -= n;
								}

								string str = Encoding.Default.GetString(array);

								str = str.TrimEnd(']', '\n', '\r');

								objFileStream.SetLength(objFileStream.Length - iCharOffset);

								strTextToAdd = str + "," + strTextToAdd.Trim('[', ']') + "]";
							}
						}
					}

					File.AppendAllText(strFilePath, strTextToAdd);
				}
				catch (Exception ex) {
					Console.WriteLine("WriterThread.Write -> ", ex.Message);
				}
				finally {
					FileTarget = "";
					Running = false;
				}
			}

			public override string ToString() {
				return m_strThreadName;
			}
		}
	}
}
