using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DDTImport
{
    public class DocumentoToImport
    {
        public string Fornitore_AgileID { get; set; }
        public string Fornitore_AgileDesc { get; set; }
        public string FornitoreCodice { get; set; }   // nome fornitore
        public string FornitoreDescrizione { get; set; }
        public string FornitorePIVA { get; set; }
        public string FornitoreCodiceFiscale { get; set; }

        public string Cliente_CodiceAssegnatoDalFornitore { get; set; }
        public string Cliente { get; set; }   // CAMPO DOVE MEMORIZZO IL CLIENTE(WUERTH)
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
        public DateTime? DataOrdine { get; set; }
        public string ArticoloCodiceGenerico { get; set; }
        public string ArticoloCodiceFornitore { get; set; }
        public string ArticoloCodiceProduttore { get; set; }
        public string ArticoloMarca { get; set; }
        public string ArticoloDescrizione { get; set; }
        public string ArticoloBarcode { get; set; }
        public string Articolo_AgileID { get; set; }
        public decimal? Qta { get; set; }
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
                
                VerificaDestinatario(documento);

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
                    "PAESE", "DATA_DDT", "NUMERO_DDT",
                    "NUMERO_POS_DDT", "CODICE_PRODOTTO", "DESCRIZIONE_PRODOTTO", "CONFEZIONE",
                    "DATA_ORDINE_CLIENTE", "NUMERO_ORDINE_CLIENTE", "NUMERO_POS_ORDINE_CLIENTE",
                    "CODICE_ARTICOLO_CLIENTE", "UNITA_DI_MISURA", "QUANTITA", "PREZZO_NETTO",
                    "UNITA_PREZZO", "PREZZO_POSIZIONE", "TOTALE_IVA", "ALIQUOTA_IVA", "DATA_ORDINE",
                    "NUMERO_ORDINE", "NUMERO_POSIZIONE_ORDINE", "CODICE_EAN", "CODICE_DEALER_COMMITTENTE",
                    "CODICE_DEALER_DESTINATARIO_MERCI", "DISPONENT", "CODICE_MERCEOLOGICO",
                    "STATO_ORIGINE_MERCE", "LETTERA_DI_VETTURA", "CENTRO_DI_COSTO", "NOTA", "LOTTO",
                    "AVVISO_DI_CONSEGNA", "NUMERO_SERIALE"
                };
                var svaiHeaders = new[] {
                    "Numero_Bolla", "Data_Bolla", "Rag_Soc_1", "Rag_Soc_2", "Indirizzo",
                    "CAP", "Localita", "Provincia","Tipo_Riga","Codice_Articolo","Marca","Descrizione_Articolo","Codice Fornitore",
                    "Quantita","Prezzo","Sconto_1","Sconto_2","Sconto_3", "Netto_Riga","IVA","Ordine"
                };
                var spazioHeaders = new[] {
                        "CODICE ARTICOLO","DESCRIZIONE", "QTA","IM. UNI. NETTO",
                        "Prezzo netto Tot.","al. iva", "N� ORDINE" // orrendo il carattere ma per il momento lo gestico così
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

        private void VerificaDestinatario(DocumentoToImport documento)
        {
            // Verifica se il destinatario è ERREBI
            if (documento.FornitoreDescrizione.Contains("ERRE-BI", StringComparison.OrdinalIgnoreCase))
            {
                documento.Verificato = true;                
            }
            else
            {
                documento.Verificato = false;
            }
        }


        //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\innerhofer E082_2024-01-0-80377.txt
        private DocumentoToImport ReadDDT_from_Innerhofer(string text)
        {
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "INNERHOFER",
                DocTipo = "DDT",
                RigheDelDoc = new List<RigaDet>()
            };

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Console.WriteLine($"Processing line: {line}");

                if (line.StartsWith("TADS"))
                {
                    documento.DocNumero = line.Substring(25, 9).Trim().TrimStart('0');
                    documento.DocData = DateTime.ParseExact(line.Substring(40, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);
                    documento.FornitoreDescrizione = line.Substring(60, 50).Trim();
                    documento.DestinazioneMerce2 = line.Substring(99, 120).Trim();
                    Console.WriteLine($"Parsed TADS: DocNumero={documento.DocNumero}, Data={documento.DocData}");
                }
                else if (line.StartsWith("RADS"))
                {
                    int orderPos = line.IndexOf("ORD");
                    var riga = new RigaDet
                    {
                        RigaTipo = "RADS",
                        ArticoloCodiceFornitore = line.Substring(54, 12).Trim(),
                        ArticoloCodiceProduttore = line.Substring(69,17).Trim(),
                        ArticoloDescrizione = line.Substring(97, 40).Trim(),
                        UM = line.Substring(140, 12).Trim(),
                        RifOrdineCliente = line.Substring(orderPos, 10).Trim(),   // offest per trovare la data da ORD
                        DataOrdine = DateTime.ParseExact(line.Substring(orderPos + 15, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None)
                    };
                    documento.RigheDelDoc.Add(riga);
                    Console.WriteLine($"Added RADS: CodFornitore={riga.ArticoloCodiceFornitore}, Desc={riga.ArticoloDescrizione}, Qta={riga.Qta}");
                }
            }

            return documento;
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

            // Legge l'header e mappa gli indici delle colonne
            var headerLine = lines[0];
            var headers = headerLine.Split(';');
            var columnIndexes = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexes[headers[i].Trim()] = i;
            }

            // Processa le righe di dati
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = line.Split(';');

                // Per la prima riga, imposta i dati del documento
                if (documento.DocNumero == null)
                {
                    documento.DocNumero = values[columnIndexes["NUMERO_DDT"]].Trim(); // NUMERO_DDT
                    if (DateTime.TryParseExact(values[columnIndexes["DATA_DDT"]].Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime docData))
                    {
                        documento.DocData = docData;
                    }
                    documento.Cliente_CodiceAssegnatoDalFornitore = values[columnIndexes["CODICE_CLIENTE"]].Trim();
                    documento.Cliente = values[columnIndexes["NOME_CLIENTE"]].Trim();
                    // Formatta l'indirizzo correttamente
                    var via = values[columnIndexes["VIA"]].Trim();
                    var cap = values[columnIndexes["CODICE_POSTALE"]].Trim();
                    var citta = values[columnIndexes["CITTA"]].Trim();
                    var provincia = values[columnIndexes["PROVINCIA"]].Trim();
                    var paese = values[columnIndexes["PAESE"]].Trim();
                    documento.DestinazioneMerce2 = $"{via}\n{cap} {citta} ({provincia})";

                    // Riferimento ordine cliente (se presente)
                    documento.RifOrdineCliente = values[columnIndexes["NUMERO_ORDINE_CLIENTE"]].Trim();
                }

                // Crea una nuova riga
                var riga = new RigaDet
                {
                    RigaNumero = int.Parse(values[columnIndexes["NUMERO_POS_DDT"]].Trim().TrimStart('0')), // NUMERO_POS_DDT senza gli zero iniziali
                    ArticoloCodiceFornitore = values[columnIndexes["CODICE_PRODOTTO"]].Trim(),           // CODICE_PRODOTTO
                    ArticoloDescrizione = values[columnIndexes["DESCRIZIONE_PRODOTTO"]].Trim(),         // DESCRIZIONE_PRODOTTO
                    Confezione = values[columnIndexes["CONFEZIONE"]].Trim(),                            // CONFEZIONE
                    UM = values[columnIndexes["UNITA_DI_MISURA"]].Trim(),                               // UNITA_DI_MISURA
                    Qta = ParseNullableDecimal(values[columnIndexes["QUANTITA"]].Trim()), // QUANTITA
                    ArticoloBarcode = values[columnIndexes["CODICE_EAN"]].Trim(),                      // CODICE_EAN
                    ArticoloCodiceGenerico = values[columnIndexes["CODICE_MERCEOLOGICO"]].Trim(),            // CODICE_MERCEOLOGICO -> ArticoloCodiceGenerico
                    DataOrdine = DateTime.ParseExact(values[columnIndexes["DATA_ORDINE_CLIENTE"]].Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None), // data ordine

                    PrezzoUnitario = ParseNullableDecimal(values[columnIndexes["PREZZO_NETTO"]].Trim()), // PREZZO_NETTO
                    PrezzoTotale = ParseNullableDecimal(values[columnIndexes["PREZZO_POSIZIONE"]].Trim()), // PREZZO_POSIZIONE
                    
                    IVAAliquota = ParseNullableDecimal(values[columnIndexes["ALIQUOTA_IVA"]].Trim()),  // ALIQUOTA_IVA

                    RifOrdineFornitore = values[columnIndexes["NUMERO_ORDINE"]].Trim()                // NUMERO_ORDINE
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

        // parsing pe rl agestione di , e .
        // C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\SVAI ddt.csv
        private decimal? ParseNullableDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Prima rimuovo tutti i punti, poi sostituisco la virgola con il punto
            value = value.Replace(".", "");
            value = value.Replace(",", ".");

            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) ? result : null;
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
                        if (DateTime.TryParseExact(fields[columnIndexes["Data_Bolla"]].Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime docData))
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
                        Qta = ParseNullableDecimal(fields[columnIndexes["Quantita"]]),
                        PrezzoUnitario = ParseNullableDecimal(fields[columnIndexes["Prezzo"]]),
                        PrezzoTotale = ParseNullableDecimal(fields[columnIndexes["Netto_Riga"]]),
                        PrezzoTotaleScontato = ParseNullableDecimal(fields[columnIndexes["Netto_Riga"]]),
                        IVAAliquota = ParseNullableDecimal(fields[columnIndexes["IVA"]]),
                        Sconto1 = ParseNullableDecimal(fields[columnIndexes["Sconto_1"]]),
                        Sconto2 = ParseNullableDecimal(fields[columnIndexes["Sconto_2"]]),
                        Sconto3 = ParseNullableDecimal(fields[columnIndexes["Sconto_3"]]),
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

                    decimal? prezzoUnitario = ParseNullableDecimal(fields[columnIndexes["IM. UNI. NETTO"]]);
                    decimal? prezzoTotale = ParseNullableDecimal(fields[columnIndexes["Prezzo netto Tot."]]);
                             
                    var riga = new RigaDet
                    {
                        RigaNumero = rigaNum++,
                        ArticoloCodiceFornitore = codiceArticolo,
                        ArticoloDescrizione = descrizione,
                        Qta = ParseNullableDecimal(fields[columnIndexes["QTA"]]),
                        PrezzoUnitario = prezzoUnitario,
                        PrezzoTotale = prezzoTotale,
                        IVAAliquota = ParseNullableDecimal(fields[columnIndexes["al. iva"]])
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
                writer.WriteLine($"Codice_Cliente: {doc.FornitoreCodice}");
                writer.WriteLine($"NOME_Cliente: {doc.Cliente}");
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
                    writer.WriteLine($"Data ordine: {riga.DataOrdine}");
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
