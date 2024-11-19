using System;
using System.Globalization;
using System.IO;

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
        public string Cliente { get; set; }
        public string NumeroDoc { get; set; }
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
        public decimal IVAAliquota { get; set; }
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
                { "Spazio", ReadDDT_from_SPAZIO },
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

                // Validazione aggiuntiva sul destinatario
                ValidateDestinatario(documento);

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
                var headers = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                 .FirstOrDefault()?
                                 .Split(';');

                // Se non ci sono intestazioni, il file non è valido
                if (headers == null || headers.Length == 0)
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
                    "CODICE ARTICOLO", "DESCRIZIONE", "QTA", "IM. UNI. NETTO",
                    "Prezzo netto Tot.", "al. iva", "N ORDINE"
                };

                // Normalizziamo le intestazioni del file per il confronto
                var normalizedHeaders = headers.Select(h => h.Trim().ToUpper()).ToList();

                // Funzione per verificare se tutte le intestazioni attese sono presenti
                bool MatchesHeaders(IEnumerable<string> expectedHeaders) =>
                    expectedHeaders.All(h => normalizedHeaders.Contains(h.ToUpper()));

                // Verifichiamo a quale fornitore corrisponde il formato
                if (MatchesHeaders(wuerthHeaders))
                    return "Wuerth";
                if (MatchesHeaders(svaiHeaders))
                    return "Svai";
                if (MatchesHeaders(innerhoferHeaders))
                    return "Innerhofer";
                if (MatchesHeaders(spazioHeaders))
                    return "Spazio";

                // Se nessun formato corrisponde completamente, lanciamo un'eccezione
                throw new InvalidOperationException("Impossibile determinare il formato del tracciato");
            }
            catch (Exception ex)
            {
                // Logghiamo l'errore e lo rilanciamo
                Console.WriteLine($"Errore nel determinare il formato: {ex.Message}");
                throw;
            }
        }


        // cerca di validare il destinatario ma non tutti lo hanno all'interno del DDT a quato pare
        // quindi si limita a notificare se è verificato o meno
        private void ValidateDestinatario(DocumentoToImport doc)
        {            
            bool isValidDestinatario = false;

            // Definiamo un array con tutte le possibili varianti del nome ERREBI
            var validNames = new[] {
                "ERREBI",
                "ERRE BI",
                "ERRE-BI",
                "ER.BI."
            };

            // controlliamo se il nome ERREBI è presente nel campo Cliente_AgileDesc del documento
            if (doc.Cliente_AgileDesc != null)
            {
                // Usiamo Any per cercare se almeno una delle varianti del nome è presente
                isValidDestinatario = validNames.Any(name =>
                    doc.Cliente_AgileDesc.ToUpper().Contains(name));
            }

            // se non abbiamo trovato ERREBI nel campo cliente controlliamo nel campo DestinazioneMerce1
            // Questo gestisce i casi in cui l'indirizzo di consegna è diverso dall'intestazione
            if (!isValidDestinatario && doc.DestinazioneMerce1 != null)
            {
                isValidDestinatario = validNames.Any(name =>
                    doc.DestinazioneMerce1.ToUpper().Contains(name));
            }

            // Gestione del risultato della validazione
            if (!isValidDestinatario)
            {
                // In caso di destinatario non valido, invece di lanciare un'eccezione stampiamo solo un messaggio di avviso(momentaneo)
                // Questo permette di continuare l'elaborazione anche se il cliente non è verificato
                Console.WriteLine("cliente non verificato");
                doc.Verificato = false;
            }
            else
            {
                Console.WriteLine("cliente verificato");
                doc.Verificato = true;
            }
        }



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

                var tipoRecord = line.Substring(0, 3);

                switch (tipoRecord)
                {
                    case "TAD":
                        documento.Cliente_CodiceAssegnatoDalFornitore = line.Substring(13, 4).Trim();
                        documento.DocNumero = line.Substring(28, 9).Trim();
                        documento.DocData = DateTime.ParseExact(line.Substring(37, 8), "yyyyMMdd", null);
                        documento.FornitoreDescrizione = line.Substring(57, 50).Trim();
                        documento.DestinazioneMerce1 = line.Substring(107, 30).Trim();
                        break;

                    case "RAD":
                        if (line[54] != 'A') continue;

                        var riga = new RigaDet
                        {
                            RigaTipo = line[54].ToString(),
                            ArticoloCodiceFornitore = line.Substring(55, 10).Trim(),
                            ArticoloCodiceProduttore = line.Substring(65, 30).Trim(),
                            ArticoloDescrizione = line.Substring(95, 50).Trim(),
                            UM = line.Substring(145, 5).Trim(),
                            Qta = decimal.Parse(line.Substring(150, 15)) / 100,
                            RifOrdineCliente = line.Substring(195, 10).Trim(),
                        };
                        documento.RigheDelDoc.Add(riga);
                        break;
                }
            }

            return documento;
        }


        private DocumentoToImport ReadDDT_from_Wuerth(string text)
        {

            Console.WriteLine("entarto per wuerth");
            // Inizializza documento con dati fissi Wuerth
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "WUERTH",
                FornitoreDescrizione = "Wuerth",
                DocTipo = "DDT"
            };

            // Splitta il testo in righe e rimuovi eventuali righe vuote
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Verifica che ci siano almeno due righe (intestazione + dati)
            if (lines.Length < 2)
            {
                throw new InvalidOperationException("Il file non contiene dati sufficienti");
            }

            // Ottieni gli indici delle colonne dall'intestazione
            var headers = lines[0].Split(';');
            var columnIndexes = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexes[headers[i].Trim()] = i;
            }

            // Verifica la presenza delle colonne necessarie
            var requiredColumns = new[]
            {
                "CODICE_CLIENTE", "NOME_CLIENTE", "VIA", "CODICE_POSTALE", "CITTA", "PROVINCIA",
                "DATA_DDT", "NUMERO_DDT", "NUMERO_POS_DDT", "CODICE_PRODOTTO", "DESCRIZIONE_PRODOTTO",
                "QUANTITA", "PREZZO_NETTO", "PREZZO_POSIZIONE", "ALIQUOTA_IVA"
            };

            foreach (var column in requiredColumns)
            {
                if (!columnIndexes.ContainsKey(column))
                {
                    throw new InvalidOperationException($"Colonna richiesta mancante: {column}");
                }
            }

            // Processa le righe di dati (salta l'intestazione)
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(';');
                if (fields.Length < headers.Length) continue;

                // Aggiorna dati cliente e testata solo alla prima riga valida
                if (documento.DocNumero == null)
                {
                    documento.Cliente_CodiceAssegnatoDalFornitore = fields[columnIndexes["CODICE_CLIENTE"]].Trim();
                    documento.Cliente_AgileDesc = fields[columnIndexes["NOME_CLIENTE"]].Trim();
                    documento.DestinazioneMerce1 = fields[columnIndexes["VIA"]].Trim();
                    documento.DestinazioneMerce2 = $"{fields[columnIndexes["CODICE_POSTALE"]].Trim()} " +
                                                 $"{fields[columnIndexes["CITTA"]].Trim()} " +
                                                 $"({fields[columnIndexes["PROVINCIA"]].Trim()})";
                    
                    documento.DocNumero = fields[columnIndexes["NUMERO_DDT"]].Trim();
                }

                try
                {
                    var riga = new RigaDet
                    {
                        RigaNumero = int.Parse(fields[columnIndexes["NUMERO_POS_DDT"]].Trim()),
                        ArticoloCodiceFornitore = fields[columnIndexes["CODICE_PRODOTTO"]].Trim(),
                        ArticoloDescrizione = fields[columnIndexes["DESCRIZIONE_PRODOTTO"]].Trim(),
                        Confezione = columnIndexes.ContainsKey("CONFEZIONE") ?
                    fields[columnIndexes["CONFEZIONE"]].Trim() : "",
                        RifOrdineCliente = columnIndexes.ContainsKey("NUMERO_ORDINE_CLIENTE") ?
                    fields[columnIndexes["NUMERO_ORDINE_CLIENTE"]].Trim() : "",
                        ArticoloCodiceGenerico = columnIndexes.ContainsKey("CODICE_ARTICOLO_CLIENTE") ?
                    fields[columnIndexes["CODICE_ARTICOLO_CLIENTE"]].Trim() : "",
                        UM = columnIndexes.ContainsKey("UNITA_DI_MISURA") ?
                    fields[columnIndexes["UNITA_DI_MISURA"]].Trim() : "",

                        // Parsing dei valori numerici con il nuovo metodo
                        Qta = ParseImporto(fields[columnIndexes["QUANTITA"]]),
                        PrezzoUnitario = ParseImporto(fields[columnIndexes["PREZZO_NETTO"]]),
                        PrezzoTotale = ParseImporto(fields[columnIndexes["PREZZO_POSIZIONE"]]),
                        IVAAliquota = ParseImporto(fields[columnIndexes["ALIQUOTA_IVA"]]),

                        RifOrdineFornitore = columnIndexes.ContainsKey("NUMERO_ORDINE") ?
                    fields[columnIndexes["NUMERO_ORDINE"]].Trim() : "",
                        ArticoloBarcode = columnIndexes.ContainsKey("CODICE_EAN") ?
                    fields[columnIndexes["CODICE_EAN"]].Trim() : ""
                    };


                    documento.RigheDelDoc.Add(riga);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore nel parsing della riga {i + 1}: {ex.Message}");
                    // Continua con la prossima riga invece di interrompere tutto il processo
                    continue;
                }
            }

            if (documento.RigheDelDoc.Count == 0)
            {
                throw new InvalidOperationException("Nessuna riga valida trovata nel documento");
            }

            return documento;
        }

        private DocumentoToImport ReadDDT_from_SVAI(string text)
        {
            Console.WriteLine("entrato per SVAI");
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SVAI",
                FornitoreDescrizione = "SVAI",
                DocTipo = "DDT"
            };

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                throw new InvalidOperationException("Il file non contiene dati sufficienti");

            var headers = lines[0].Split(';');
            var columnIndexes = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
                columnIndexes[headers[i].Trim()] = i;

            var requiredColumns = new[]
            {
                "Numero_Bolla", "Data_Bolla", "Rag_Soc_1", "Codice_Articolo",
                "Descrizione_Articolo", "Quantita", "Prezzo", "Netto_Riga", "IVA"
            };

            foreach (var column in requiredColumns)
                if (!columnIndexes.ContainsKey(column))
                    throw new InvalidOperationException($"Colonna richiesta mancante: {column}");

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(';');
                if (fields.Length < headers.Length) continue;

                try
                {
                    // Gestione del cliente combinando Rag_Soc_1 e Rag_Soc_2 se presente
                    string cliente = fields[columnIndexes["Rag_Soc_1"]].Trim();
                    if (columnIndexes.ContainsKey("Rag_Soc_2"))
                    {
                        string ragSoc2 = fields[columnIndexes["Rag_Soc_2"]].Trim();
                        if (!string.IsNullOrWhiteSpace(ragSoc2))
                        {
                            cliente = $"{cliente} {ragSoc2}";
                        }
                    }

                    var riga = new RigaDet
                    {
                        RigaNumero = i,
                        Cliente = cliente,
                        NumeroDoc = fields[columnIndexes["Numero_Bolla"]].Trim(),                        
                        RigaTipo = columnIndexes.ContainsKey("Tipo_Riga") ?
                            fields[columnIndexes["Tipo_Riga"]].Trim() : "",
                        ArticoloCodiceFornitore = fields[columnIndexes["Codice_Articolo"]].Trim(),
                        ArticoloMarca = columnIndexes.ContainsKey("Marca") ?
                            fields[columnIndexes["Marca"]].Trim() : "",
                        ArticoloDescrizione = fields[columnIndexes["Descrizione_Articolo"]].Trim(),
                        ArticoloCodiceGenerico = columnIndexes.ContainsKey("Codice Fornitore") ?
                            fields[columnIndexes["Codice Fornitore"]].Trim() : "",
                        Qta = ParseImporto(fields[columnIndexes["Quantita"]]),
                        PrezzoUnitario = ParseImporto(fields[columnIndexes["Prezzo"]]),
                        PrezzoTotale = ParseImporto(fields[columnIndexes["Netto_Riga"]]),
                        PrezzoTotaleScontato = ParseImporto(fields[columnIndexes["Netto_Riga"]]),
                        IVAAliquota = ParseImporto(fields[columnIndexes["IVA"]]),
                        Sconto1 = columnIndexes.ContainsKey("Sconto_1") ?
                            ParseImporto(fields[columnIndexes["Sconto_1"]]) : 0,
                        Sconto2 = columnIndexes.ContainsKey("Sconto_2") ?
                            ParseImporto(fields[columnIndexes["Sconto_2"]]) : 0,
                        Sconto3 = columnIndexes.ContainsKey("Sconto_3") ?
                            ParseImporto(fields[columnIndexes["Sconto_3"]]) : 0,
                        RifOrdineFornitore = columnIndexes.ContainsKey("Ordine") ?
                            fields[columnIndexes["Ordine"]].Trim() : ""
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
                throw new InvalidOperationException("Nessuna riga valida trovata nel documento");

            return documento;
        }

        private DocumentoToImport ReadDDT_from_SPAZIO(string text)
        {
            Console.WriteLine("entrato per SPAZIO");
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SPAZIO",
                FornitoreDescrizione = "Spazio",
                DocTipo = "DDT"
            };

            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                throw new InvalidOperationException("Il file non contiene dati sufficienti");
            }

            var headers = lines[0].Split(';');
            var columnIndexes = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                columnIndexes[headers[i].Trim()] = i;
            }

            var requiredColumns = new[]
            {
                "CODICE ARTICOLO", "DESCRIZIONE", "QTA", "IM. UNI. NETTO",
                "Prezzo netto Tot.", "al. iva", "N ORDINE"
            };

            foreach (var column in requiredColumns)
            {
                if (!columnIndexes.ContainsKey(column))
                {
                    throw new InvalidOperationException($"Colonna richiesta mancante: {column}");
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(';');
                if (fields.Length < headers.Length) continue;

                try
                {
                    var riga = new RigaDet
                    {
                        RigaNumero = i,
                        ArticoloCodiceFornitore = fields[columnIndexes["CODICE ARTICOLO"]].Trim(),
                        ArticoloDescrizione = fields[columnIndexes["DESCRIZIONE"]].Trim(),
                        Qta = ParseImporto(fields[columnIndexes["QTA"]]),
                        PrezzoUnitario = ParseImporto(fields[columnIndexes["IM. UNI. NETTO"]]),
                        PrezzoTotale = ParseImporto(fields[columnIndexes["Prezzo netto Tot."]]),
                        IVAAliquota = ParseImporto(fields[columnIndexes["al. iva"]]),
                        RifOrdineFornitore = fields[columnIndexes["N ORDINE"]].Trim()
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

            // Gestione caso con due punti 
            if (value.Count(c => c == '.') > 1)
            {
                // Trova l'ultimo punto e sostituiscilo con virgola, rimuovi gli altri punti
                int lastDotIndex = value.LastIndexOf('.');
                value = value.Substring(0, lastDotIndex).Replace(".", "") +
                       "," +
                       value.Substring(lastDotIndex + 1);
            }
            else if (value.Contains("."))
            {
                // Se c'è un solo punto, sostituiscilo con la virgola
                value = value.Replace(".", ",");
            }

            // Prova a parsare con la cultura corrente (virgola come separatore decimale)
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal result))
                return result;

            // Se fallisce, prova con la cultura invariante
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
                return result;

            Console.WriteLine($"Impossibile parsare il valore '{value}' come numero decimale. Uso 0 come default.");
            return 0;
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

                    string scelta = Console.ReadLine();
                    string contenutoFile = File.ReadAllText(filePath);
                    DocumentoToImport documento;

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
                    writer.WriteLine($"Cliente: {riga.Cliente}");
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
    } //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Innerhofer DDT 23-24.csv  
}//C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Wuerth CSV - DDT_8826546665_20240503_154226.csv
 //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\SVAI ddt.csv
 //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Spazio-esportazione (4).csv
 //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\innerhofer E082_2024-01-0-80377.txt