using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DDTImport
{
    public class DocumentoToImport
    {
        public string Fornitore_AgileID { get; set; }
        public string Fornitore_AgileDesc { get; set; }
        public string FornitoreCodice { get; set; }
        public string FornitoreDescrizione { get; set; }
        public string FornitorePIVA { get; set; }
        public string FornitoreCodiceFiscale { get; set; }

        public string Cliente_CodiceAssegnatoDalFornitore { get; set; }
        public string Cliente_AgileID { get; set; }
        public string Cliente_AgileDesc { get; set; }
        public string Cliente_PIVA { get; set; }
        public string Cliente_CodiceFiscale { get; set; }

        public string DocTipo { get; set; }
        public string DocNumero { get; set; }
        public DateTime DocData { get; set; }
        public string DocBarcode { get; set; }
        public string RifOrdineFornitore { get; set; }
        public string RifOrdineCliente { get; set; }
        public string DestinazioneMerce1 { get; set; }
        public string DestinazioneMerce2 { get; set; }
        public string Note { get; set; }

        public string TrasportoData { get; set; }
        public string TrasportoNote { get; set; }
        public bool Verificato { get; set; }
        public List<RigaDet> RigheDelDoc { get; set; } = new List<RigaDet>();
    }

    public class RigaDet
    {
        public int RigaNumero { get; set; }
        public string RigaTipo { get; set; }
        public string NumeroDoc { get; set; }
        public string CodiceCliente { get; set; }
        public DateTime DocData { get; set; }
        public string ArticoloCodiceGenerico { get; set; }
        public string ArticoloCodiceFornitore { get; set; }
        public string ArticoloCodiceProduttore { get; set; }
        public string ArticoloMarca { get; set; }
        public string ArticoloDescrizione { get; set; }
        public string ArticoloBarcode { get; set; }
        public string Articolo_AgileID { get; set; }
        public decimal Qta { get; set; }
        public string UM { get; set; }
        public string Confezione { get; set; }
        public decimal? PrezzoUnitario { get; set; }
        public decimal? Sconto1 { get; set; }
        public decimal? Sconto2 { get; set; }
        public decimal? Sconto3 { get; set; }
        public decimal? PrezzoTotale { get; set; }
        public decimal? PrezzoTotaleScontato { get; set; }
        public string IVACodice { get; set; }
        public decimal? IVAAliquota { get; set; }
        public string RifOrdineFornitore { get; set; }
        public string RifOrdineCliente { get; set; }
        public string DestinazioneMerce { get; set; }
    }

    public class DR_Contab_ImportDDT
    {
        private readonly Dictionary<string, Func<string, DocumentoToImport>> _formatReaders;

        public DR_Contab_ImportDDT()
        {
            _formatReaders = new Dictionary<string, Func<string, DocumentoToImport>>
            {
                { "Innerhofer", ReadDDT_from_Innerhofer },
                { "Wuerth", ReadDDT_from_Wuerth },
                { "Spazio", ReadDDT_from_Spazio },
                { "Svai", ReadDDT_from_SVAI }
            };
        }

        // Modifica al metodo ReadDDT nella classe DR_Contab_ImportDDT
        public DocumentoToImport ReadDDT(string fileName, string text, string formatoDelTracciato = null)
        {
            Console.WriteLine($"\nInizio elaborazione {fileName}...");

            try
            {
                // Se non è specificato il formato, lo rileva automaticamente
                if (string.IsNullOrEmpty(formatoDelTracciato))
                {
                    Console.WriteLine("Rilevamento automatico formato...");
                    formatoDelTracciato = DeterminaFormatoTracciato(text);
                    Console.WriteLine($"Formato rilevato: {formatoDelTracciato}");
                }
                else
                {
                    Console.WriteLine($"Utilizzo formato specificato: {formatoDelTracciato}");
                }

                // Usa il formato per selezionare il parser appropriato
                var documento = _formatReaders[formatoDelTracciato](text);
                Console.WriteLine($"Parsing completato. Righe elaborate: {documento.RigheDelDoc.Count}");

                // Verifica che ci siano righe
                if (documento.RigheDelDoc.Count == 0)
                {
                    throw new InvalidOperationException("Nessuna riga trovata nel documento");
                }

                return documento;
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException("Formato del tracciato non supportato");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore nell'elaborazione del DDT: {ex.Message}");
                throw;
            }
        }

        // Funazione che determina il formato del DDT tramite l'inetstazione
        private string DeterminaFormatoTracciato(string text)
        {
            try
            {

                // Dividiamo il testo in righe e prendiamo la prima riga (intestazione)
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var firstLine = lines.FirstOrDefault();

                // Se non ci sono righe, il file non è valido
                if (string.IsNullOrWhiteSpace(firstLine))
                    throw new InvalidOperationException("File vuoto o non valido");

                // Controlliamo se la riga usa il separatore ';'
                bool usesSemicolon = firstLine.Contains(';');

                // Normalizziamo la prima riga come fatto nella funzione ReadDDT_from_Spazio
                var headerLine = firstLine
                    .Replace("\"", "")
                    .Replace("\t", "")
                    .Replace("\u00A0", " ");

                // Dividiamo le intestazioni e le normalizziamo
                var headers = usesSemicolon
                    ? headerLine.Split(';')
                        .Select(h => new string(h.Where(c => !char.IsControl(c)).ToArray()))
                        .Select(h => h.Trim())
                        .ToList()
                    : headerLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(h => h.Trim())
                        .ToList();
                                

                // Se non ci sono intestazioni dopo la divisione, il file non è valido
                if (headers == null || headers.Count == 0)
                    throw new InvalidOperationException("File vuoto o non valido");

                // Definiamo le intestazioni attese per ciascun fornitore
                var wuerthHeaders = new[] {
                    "CODICE_CLIENTE", "NOME_CLIENTE", "VIA", "CODICE_POSTALE", "CITTA", "PROVINCIA",
                    "PAESE", "DATA_DDT", "NUMERO_DDT"
                };
                var svaiHeaders = new[] {
                    "Numero_Bolla", "Data_Bolla", "Rag_Soc_1", "Rag_Soc_2", "Indirizzo",
                    "CAP", "Localita", "Provincia"
                };
                var innerhoferHeaders = new[] {
                    "TipoRecord", "CodiceIdentificativo", "CodiceCliente", "NumeroDocumento", "Data", "NumeroRiga", "TipoRiga",
                    "CodiceArticolo", "CodiceProdotto", "DescrizioneArticolo", "UnitaMisura", "Quantita", "NumeroOrdine", "DataOrdine"
                };
                var spazioHeaders = new[] {
                        "CODICE ARTICOLO",
                        "DESCRIZIONE",
                        "QTA",
                        "IM. UNI. NETTO",
                        "Prezzo netto Tot.",
                        "al. iva",
                        "N� ORDINE" 
                    };

                // Funzione per verificare se tutte le intestazioni attese sono presenti
                bool MatchesHeaders(IEnumerable<string> expectedHeaders, string formatName)
                {
                    int matches = 0;
                    int total = 0;

                    foreach (var expected in expectedHeaders)
                    {
                        total++;
                        bool found = headers.Any(h => h.Equals(expected, StringComparison.OrdinalIgnoreCase));
                        if (found) matches++;
                    }

                    bool isMatch = matches == total;
                    return isMatch;
                }


                // Se il file non usa il separatore ';' e non contiene header standard, consideriamo Innerhofer
                if (!usesSemicolon)
                {
                    Console.WriteLine("\nFile senza separatore ';' -> Formato Innerhofer");
                    return "Innerhofer";
                }

                // Verifichiamo a quale fornitore corrisponde il formato
                if (MatchesHeaders(wuerthHeaders, "Wuerth"))
                    return "Wuerth";
                if (MatchesHeaders(svaiHeaders, "Svai"))
                    return "Svai";
                if (MatchesHeaders(innerhoferHeaders, "Innerhofer"))
                    return "Innerhofer";
                if (MatchesHeaders(spazioHeaders, "Spazio"))
                    return "Spazio";

                Console.WriteLine("\nNessun formato riconosciuto!");
                throw new InvalidOperationException("Impossibile determinare il formato del tracciato");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErrore nel determinare il formato: {ex.Message}");
                throw;
            }
        }



        //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Spazio-esportazione (4).csv
        private DocumentoToImport ReadDDT_from_Innerhofer(string text)
        {
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "INNERHOFER",
                FornitoreDescrizione = "Innerhofer",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.Length < 4) continue;

                var tipoRecord = line.Substring(0, 4).Trim();

                switch (tipoRecord)
                {
                    case "TADS":
                        try
                        {
                            string numeroCompleto = line.Substring(31, 9).Trim();
                            documento.DocNumero = numeroCompleto.TrimStart('0');

                            string dataStr = line.Substring(40, 8).Trim();
                            if (!string.IsNullOrWhiteSpace(dataStr))
                            {
                                documento.DocData = DateTime.ParseExact(dataStr, "yyyyMMdd", CultureInfo.InvariantCulture);
                            }

                            documento.Cliente_AgileDesc = line.Substring(50, 40).Trim();

                            var indirizzo = line.Substring(98, 77).Trim();
                            documento.DestinazioneMerce2 = string.Join(" ", indirizzo.Split(new[] { ' ' },
                                StringSplitOptions.RemoveEmptyEntries));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore nell'elaborazione della Testata: {ex.Message}");
                            throw;
                        }
                        break;

                    case "RADS":
                        try
                        {
                            string codiceFornitore = line.Substring(65, 9).Trim();  // Per ottenere 002084130
                            Console.WriteLine($"Codice Fornitore estratto: {codiceFornitore}");

                            string codiceProduttore = line.Substring(90, 17).Trim(); // Per ottenere 40152116160072205
                            Console.WriteLine($"Codice Produttore estratto: {codiceProduttore}");

                            string descrizione = line.Substring(110, 50).Trim(); // Per ottenere Sanpress Tubo AISI 444 18x1mm
                            Console.WriteLine($"Descrizione estratta: {descrizione}");

                            string qtaStr = line.Substring(180, 25).Trim(); // Per ottenere la quantità
                            Console.WriteLine($"Quantità string: {qtaStr}");

                            var riga = new RigaDet
                            {
                                RigaTipo = "RADS",
                                ArticoloCodiceFornitore = codiceFornitore,
                                ArticoloCodiceProduttore = codiceProduttore,
                                ArticoloDescrizione = descrizione,
                                UM = "MTR",
                                Qta = ParseQuantity(qtaStr),
                                RifOrdineCliente = line.Substring(194, 12).Trim()
                            };

                            if (!string.IsNullOrWhiteSpace(riga.ArticoloCodiceFornitore) &&
                                !string.IsNullOrWhiteSpace(riga.ArticoloDescrizione))
                            {
                                documento.RigheDelDoc.Add(riga);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore nell'elaborazione della Riga: {ex.Message}");
                            throw;
                        }
                        break;

                    case "CADS":
                        break;

                    default:
                        Console.WriteLine($"Tipo record non riconosciuto: {tipoRecord}");
                        break;
                }
            }

            return documento;
        }

        private decimal ParseQuantity(string qtaStr)
        {
            if (string.IsNullOrWhiteSpace(qtaStr)) return 0;

            qtaStr = new string(qtaStr.Where(char.IsDigit).ToArray());

            if (decimal.TryParse(qtaStr, out decimal result))
            {
                return result / 10000m;
            }

            return 0;
        }

        private DocumentoToImport ReadDDT_from_Wuerth(string text)
        {
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "WUERTH",
                FornitoreDescrizione = "Wuerth",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            // Legge le righe del CSV
            var lines = text.Split('\n');
            if (lines.Length < 2) return documento; // Verifica che ci sia almeno l'header e una riga

            // Ignora la prima riga (header)
            var headerLine = lines[0];
            var headers = headerLine.Split(';');

            // Processa le righe di dati
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = line.Split(';');

                // Per la prima riga, imposta i dati del documento
                if (documento.DocNumero == null)
                {
                    documento.DocNumero = values[8].Trim(); // NUMERO_DDT
                    if (DateTime.TryParseExact(values[7].Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime docData))
                    {
                        documento.DocData = docData;
                    }

                    // Formatta l'indirizzo correttamente
                    var via = values[2].Trim();
                    var cap = values[3].Trim();
                    var citta = values[4].Trim();
                    var provincia = values[5].Trim();
                    var paese = values[6].Trim();
                    documento.DestinazioneMerce2 = $"{via}\n{cap} {citta} ({provincia})";

                    // Riferimento ordine cliente (se presente)
                    documento.RifOrdineCliente = values[14].Trim(); // NUMERO_ORDINE_CLIENTE
                }

                // Crea una nuova riga
                var riga = new RigaDet
                {
                    RigaNumero = int.Parse(values[9].Trim().TrimStart('0')), // NUMERO_POS_DDT senza gli zero iniziali
                    ArticoloCodiceFornitore = values[10].Trim(),            // CODICE_PRODOTTO
                    ArticoloDescrizione = values[11].Trim(),                // DESCRIZIONE_PRODOTTO
                    Confezione = values[12].Trim(),                         // CONFEZIONE
                    UM = values[17].Trim(),                                 // UNITA_DI_MISURA
                    Qta = decimal.Parse(values[18].Trim(), CultureInfo.InvariantCulture), // QUANTITA
                    ArticoloBarcode = values[27].Trim(),                    // CODICE_EAN

                    // Prezzi
                    PrezzoUnitario = ParseNullableDecimal(values[19].Trim()), // PREZZO_NETTO
                    PrezzoTotale = ParseNullableDecimal(values[21].Trim()),   // PREZZO_POSIZIONE

                    // IVA
                    IVAAliquota = ParseNullableDecimal(values[23].Trim()),    // ALIQUOTA_IVA

                    // Riferimenti ordine
                    RifOrdineFornitore = values[24].Trim()                    // NUMERO_ORDINE
                };

                // Se c'è un prezzo unitario, calcola il prezzo totale scontato
                if (riga.PrezzoUnitario.HasValue)
                {
                    riga.PrezzoTotaleScontato = riga.PrezzoTotale;
                }

                documento.RigheDelDoc.Add(riga);
            }

            return documento;
        }

        private decimal? ParseNullableDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return decimal.TryParse(value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : null;
        }


        private DocumentoToImport ReadDDT_from_SVAI(string text)
        {
            Console.WriteLine("Entrato per SVAI");
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SVAI",
                FornitoreDescrizione = "SVAI",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                throw new InvalidOperationException("Il file non contiene dati sufficienti");

            var headers = lines[0].Split(';');
            var columnIndexes = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexes[headers[i].Trim()] = i;
            }

            bool firstRow = true;
            int rigaNum = 1;

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(';');
                if (fields.Length < headers.Length) continue;

                try
                {
                    // Solo dalla prima riga prendiamo i dati del documento
                    if (firstRow)
                    {
                        documento.DocNumero = fields[columnIndexes["Numero_Bolla"]].Trim();

                        // Parsing della data
                        if (DateTime.TryParseExact(fields[columnIndexes["Data_Bolla"]].Trim(),
                            "dd/MM/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out DateTime docData))
                        {
                            documento.DocData = docData;
                        }
                                                                        
                        firstRow = false;
                    }

                    var riga = new RigaDet
                    {
                        RigaNumero = rigaNum++,
                        RigaTipo = fields[columnIndexes["Tipo_Riga"]].Trim(),
                        ArticoloCodiceFornitore = fields[columnIndexes["Codice_Articolo"]].Trim(),
                        ArticoloCodiceGenerico = fields[columnIndexes["Codice Fornitore"]].Trim(),
                        ArticoloMarca = fields[columnIndexes["Marca"]].Trim(),
                        ArticoloDescrizione = fields[columnIndexes["Descrizione_Articolo"]].Trim(),
                        Qta = ParseImporto(fields[columnIndexes["Quantita"]]),
                        PrezzoUnitario = ParseImporto(fields[columnIndexes["Prezzo"]]),
                        PrezzoTotale = ParseImporto(fields[columnIndexes["Netto_Riga"]]),
                        PrezzoTotaleScontato = ParseImporto(fields[columnIndexes["Netto_Riga"]]),
                        IVAAliquota = ParseImporto(fields[columnIndexes["IVA"]]),
                        Sconto1 = ParseImporto(fields[columnIndexes["Sconto_1"]]),
                        Sconto2 = ParseImporto(fields[columnIndexes["Sconto_2"]]),
                        Sconto3 = ParseImporto(fields[columnIndexes["Sconto_3"]]),
                        RifOrdineFornitore = fields[columnIndexes["Ordine"]].Trim(),
                    };

                    documento.RigheDelDoc.Add(riga);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore nel parsing della riga {i + 1}: {ex.Message}");
                    continue;
                }
            }

            if (documento.RigheDelDoc.Count == 0)
            {
                throw new InvalidOperationException("Nessuna riga valida trovata nel documento");
            }

            return documento;
        }
        //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Spazio-esportazione (4).csv
        private DocumentoToImport ReadDDT_from_Spazio(string text)
        {
            Console.WriteLine("Entrato per SPAZIO");
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SPAZIO",
                FornitoreDescrizione = "SPAZIO",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                throw new InvalidOperationException("Il file non contiene dati sufficienti");

            
            // Imposta la data di oggi come data documento se non specificata altrimenti
            documento.DocData = DateTime.Today;

            var headerLine = lines[0]
                .Replace("\"", "")
                .Replace("\t", "")
                .Replace("\u00A0", " ");

            var headers = headerLine.Split(';')
                .Select(h => new string(h.Where(c => !char.IsControl(c)).ToArray()))
                .Select(h => h.Trim())
                .ToList();

            var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                var cleanHeader = headers[i].Trim();
                columnIndexes[cleanHeader] = i;
            }

            var requiredColumns = new[]
            {
                "CODICE ARTICOLO",
                "DESCRIZIONE",
                "QTA",
                "IM. UNI. NETTO",
                "Prezzo netto Tot.",
                "al. iva",
                "N� ORDINE"
            };

            var missingColumns = requiredColumns
                .Where(col => !columnIndexes.ContainsKey(col))
                .ToList();

            if (missingColumns.Any())
            {
                throw new InvalidOperationException($"Colonne richieste mancanti: {string.Join(", ", missingColumns)}");
            }

            int rigaNum = 1;

            // Processa le righe di dati
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var line = lines[i]
                        .Replace("\"", "")
                        .Replace("\t", "")
                        .Replace("\u00A0", " ");

                    var fields = line.Split(';')
                        .Select(f => f.Trim())
                        .ToArray();

                    if (fields.Length < headers.Count) continue;

                    string codiceArticolo = fields[columnIndexes["CODICE ARTICOLO"]]
                        .Replace(" ", "")  // Rimuove tutti gli spazi
                        .Trim();

                    string descrizione = fields[columnIndexes["DESCRIZIONE"]]
                        .Replace("  ", " ")  // Rimuove spazi doppi
                        .Trim();
                                        
                    decimal prezzoUnitario = ParseImporto(fields[columnIndexes["IM. UNI. NETTO"]].Replace("?", ""));
                    decimal prezzoTotale = ParseImporto(fields[columnIndexes["Prezzo netto Tot."]].Replace("?", ""));

                    // Calcola lo sconto se i prezzi sono diversi
                    string sconti = "";
                    if (prezzoUnitario > 0 && prezzoTotale > 0 && prezzoUnitario != prezzoTotale)
                    {
                        decimal scontoPercentuale = (1 - (prezzoTotale / prezzoUnitario)) * 100;
                        sconti = $"{Math.Round(scontoPercentuale, 2)}%";
                    }

                    var riga = new RigaDet
                    {
                        RigaNumero = rigaNum++,
                        ArticoloCodiceFornitore = codiceArticolo,
                        ArticoloDescrizione = descrizione,
                        Qta = ParseImporto(fields[columnIndexes["QTA"]]),
                        PrezzoUnitario = prezzoUnitario,
                        PrezzoTotale = prezzoTotale,
                        PrezzoTotaleScontato = prezzoTotale,
                        IVAAliquota = ParseImporto(fields[columnIndexes["al. iva"]].Replace("%", ""))
                    };

                    documento.RigheDelDoc.Add(riga);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore nel parsing della riga {i + 1}: {ex.Message}");
                    continue;
                }
            }

            if (documento.RigheDelDoc.Count == 0)
            {
                throw new InvalidOperationException("Nessuna riga valida trovata nel documento");
            }

            return documento;
        }


        // Sistema i numeri con . e ,
        private decimal ParseImporto(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            // Rimuovi spazi e caratteri non necessari
            value = value.Trim();

            try
            {
                // Rimuovi eventuali simboli di valuta (€, $, ecc.)
                value = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray());

                // Se il numero contiene sia punto che virgola
                if (value.Contains(".") && value.Contains(","))
                {
                    // Se la virgola viene dopo il punto, è il separatore decimale italiano
                    // es: 1.175,00 -> la virgola è il vero separatore decimale
                    if (value.IndexOf('.') < value.IndexOf(','))
                    {
                        // Rimuovi i punti e mantieni la virgola
                        value = value.Replace(".", "");
                    }
                    // Se il punto viene dopo la virgola, è il separatore decimale inglese
                    // es: 1,175.00 -> il punto è il vero separatore decimale
                    else
                    {
                        // Rimuovi le virgole e converti il punto in virgola
                        value = value.Replace(",", "").Replace(".", ",");
                    }
                }
                // Se c'è solo il punto
                else if (value.Contains("."))
                {
                    value = value.Replace(".", ",");
                }

                // Prova a parsare con la cultura corrente (virgola come separatore decimale)
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal result))
                    return Math.Round(result, 2);

                // Se fallisce, prova con la cultura invariante
                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                    return Math.Round(result, 2);

                Console.WriteLine($"Impossibile parsare il valore '{value}' come importo. Uso 0 come default.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante il parsing dell'importo '{value}': {ex.Message}");
                return 0;
            }
        }
    }


    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("=== IMPORT DDT ===");
            var reader = new DR_Contab_ImportDDT();

            while (true)
            {
                Console.WriteLine("\nInserisci il percorso del file DDT (o 'exit' per uscire):");
                string filePath = Console.ReadLine();

                if (filePath?.ToLower() == "exit")
                    break;

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("File non trovato. Riprova.");
                    continue;
                }

                try
                {
                    string fileName = Path.GetFileName(filePath);
                    Console.WriteLine($"\nElaborazione del file: {fileName}");
                    Console.WriteLine("\nScegli il formato del file:");
                    Console.WriteLine("1. Innerhofer");
                    Console.WriteLine("2. Wuerth");
                    Console.WriteLine("3. Spazio");
                    Console.WriteLine("4. Svai");
                    Console.WriteLine("5. Rilevamento automatico");

                    
                    string contenutoFile = File.ReadAllText(filePath);
                    DocumentoToImport documento;

                    string scelta = Console.ReadLine();
                    switch (scelta)
                    {
                        case "1":
                        case "2":
                        case "3":
                        case "4":
                            string formato = scelta switch
                            {
                                "1" => "Innerhofer",
                                "2" => "Wuerth",
                                "3" => "Spazio",
                                "4" => "Svai",
                                _ => throw new InvalidOperationException("Scelta non valida.")
                            };
                            documento = reader.ReadDDT(fileName, contenutoFile, formato);
                            break;
                        case "5":
                            documento = reader.ReadDDT(fileName, contenutoFile);
                            break;
                        default:
                            Console.WriteLine("Scelta non valida.");
                            continue;
                    }

                    WriteDDTToFile(documento);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore: {ex.Message}");
                }
            }
        }

        // Metodo per scrivere in un file output per la verifica (Momentaneo)
        public static void WriteDDTToFile(DocumentoToImport doc)
        {
            string outputPath = "output.txt";
            using (StreamWriter writer = new StreamWriter(outputPath))
            {
                writer.WriteLine("=== DATI DOCUMENTO ===");
                writer.WriteLine($"Fornitore: {doc.FornitoreDescrizione}");
                writer.WriteLine($"Tipo Documento: {doc.DocTipo}");
                writer.WriteLine($"Numero: {doc.DocNumero}");
                writer.WriteLine($"Data: {doc.DocData:dd/MM/yyyy}");
                writer.WriteLine($"Destinazione: {doc.DestinazioneMerce1}");
                writer.WriteLine($"             {doc.DestinazioneMerce2}");
                writer.WriteLine($"Riferimento Ordine Fornitore: {doc.RifOrdineFornitore}");
                writer.WriteLine($"Riferimento Ordine Cliente: {doc.RifOrdineCliente}");
                writer.WriteLine($"Note: {doc.Note}");
                writer.WriteLine($"Trasporto Data: {doc.TrasportoData}");
                writer.WriteLine($"Trasporto Note: {doc.TrasportoNote}");
                writer.WriteLine($"Documento Verificato: {doc.Verificato}");

                // Scrivi i dettagli delle prime 3 righe
                for (int i = 0; i < Math.Min(3, doc.RigheDelDoc.Count); i++)
                {
                    var riga = doc.RigheDelDoc[i];
                    writer.WriteLine($"\n=== RIGA {i + 1} ===");
                    writer.WriteLine($"Numero Riga: {riga.RigaNumero}");
                    writer.WriteLine($"Tipo Riga: {riga.RigaTipo}");
                    writer.WriteLine($"Articolo - Codice Generico: {riga.ArticoloCodiceGenerico}");
                    writer.WriteLine($"Articolo - Codice Fornitore: {riga.ArticoloCodiceFornitore}");
                    writer.WriteLine($"Articolo - Codice Produttore: {riga.ArticoloCodiceProduttore}");
                    writer.WriteLine($"Articolo - Marca: {riga.ArticoloMarca}");
                    writer.WriteLine($"Articolo - Descrizione: {riga.ArticoloDescrizione}");
                    writer.WriteLine($"Articolo - Barcode: {riga.ArticoloBarcode}");
                    writer.WriteLine($"Articolo - Agile ID: {riga.Articolo_AgileID}");
                    writer.WriteLine($"Quantità: {riga.Qta}");
                    writer.WriteLine($"Unità di Misura: {riga.UM}");
                    writer.WriteLine($"Confezione: {riga.Confezione}");
                    writer.WriteLine($"Prezzo Unitario: {riga.PrezzoUnitario:C2}");
                    writer.WriteLine($"Sconti: {riga.Sconto1}% + {riga.Sconto2}% + {riga.Sconto3}%");
                    writer.WriteLine($"Prezzo Totale: {riga.PrezzoTotale:C2}");
                    writer.WriteLine($"Prezzo Totale Scontato: {riga.PrezzoTotaleScontato:C2}");
                    writer.WriteLine($"IVA Codice: {riga.IVACodice}");
                    writer.WriteLine($"IVA Aliquota: {riga.IVAAliquota}%");
                    writer.WriteLine($"Rif. Ordine Fornitore: {riga.RifOrdineFornitore}");
                    writer.WriteLine($"Rif. Ordine Cliente: {riga.RifOrdineCliente}");
                    writer.WriteLine($"Destinazione Merce: {riga.DestinazioneMerce}");
                }

                writer.WriteLine($"\nTotale righe nel documento: {doc.RigheDelDoc.Count}");
            }
        }
    }
}//C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Wuerth CSV - DDT_8826546665_20240503_154226.csv
 //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\SVAI ddt.csv
 //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Spazio-esportazione (4).csv
 //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\innerhofer E082_2024-01-0-80377.txt
