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

        public List<RigaDet> RigheDelDoc { get; set; } = new List<RigaDet>();
    }

    public class RigaDet
    {
        public int RigaNumero { get; set; }
        public string RigaTipo { get; set; }
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
                // Poi la dividiamo in colonne usando il punto e virgola come separatore
                var headers = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                 .FirstOrDefault()?
                                 .Split(';');

                // Se non ci sono intestazioni, il file non è valido
                if (headers == null || headers.Length == 0)
                    throw new InvalidOperationException("File vuoto o non valido");

                // Definiamo le intestazioni che ci aspettiamo per ogni fornitore
                // Queste sono le colonne caratteristiche di ciascun formato
                var wuerthHeaders = new[] {
                    "CODICE_CLIENTE", "NOME_CLIENTE", "VIA", "CODICE_POSTALE", "CITTA", "PROVINCIA",
                    "PAESE", "DATA_DDT", "NUMERO_DDT"
                };
                var svaiHeaders = new[] {
                    "Numero_Bolla", "Data_Bolla", "Rag_Soc_1", "Rag_Soc_2", "Indirizzo",
                    "CAP", "Localita", "Provincia"
                };
                var innerhoferHeaders = new[] {
                    "Codice articolo", "Codice interno", "Descrizione articolo",
                    "Prezzo unico/netto", "Quantità", "Prezzo totale"
                };
                // Spazio è commentato perché non implementato
                /* var spazioHeaders = new[] { 
                };*/

                // Normalizziamo le intestazioni del file per il confronto:
                // - Rimuoviamo gli spazi iniziali e finali
                // - Convertiamo tutto in maiuscolo per un confronto case-insensitive
                var normalizedHeaders = headers.Select(h => h.Trim().ToUpper()).ToList();

                // Contiamo quante intestazioni del file corrispondono a quelle attese per ogni fornitore
                // Più alto è il numero di corrispondenze, più è probabile che sia quel formato
                int wuerthMatches = wuerthHeaders.Count(h =>
                    normalizedHeaders.Contains(h.ToUpper()));
                int svaiMatches = svaiHeaders.Count(h =>
                    normalizedHeaders.Contains(h.ToUpper()));
                int innerhoferMatches = innerhoferHeaders.Count(h =>
                    normalizedHeaders.Contains(h.ToUpper()));

                // Calcoliamo la percentuale di corrispondenza per ogni fornitore
                // Es: se su 10 colonne attese ne troviamo 7, la percentuale è 0.7 (70%)
                double wuerthPercentage = (double)wuerthMatches / wuerthHeaders.Length;
                double svaiPercentage = (double)svaiMatches / svaiHeaders.Length;
                double innerhoferPercentage = (double)innerhoferMatches / innerhoferHeaders.Length;
                // Spazio è impostato a 0 per disabilitare il rilevamento automatico finchè non avremmo un loro DDT
                double spazioPercentage = 0;

                // Definiamo una soglia minima del 70% per considerare una corrispondenza valida
                const double threshold = 0.7;

                // Determiniamo il fornitore in base alla maggiore percentuale di corrispondenza
                // Per essere valida, la percentuale deve:
                // 1. Superare la soglia minima (threshold)
                // 2. Essere la più alta tra tutte le percentuali
                if (wuerthPercentage > threshold && wuerthPercentage >= Math.Max(Math.Max(svaiPercentage, innerhoferPercentage), spazioPercentage))
                    return "Wuerth";
                if (svaiPercentage > threshold && svaiPercentage >= Math.Max(Math.Max(wuerthPercentage, innerhoferPercentage), spazioPercentage))
                    return "Svai";
                if (innerhoferPercentage > threshold && innerhoferPercentage >= Math.Max(Math.Max(wuerthPercentage, svaiPercentage), spazioPercentage))
                    return "Innerhofer";

                // Se il metodo delle percentuali fallisce, proviamo a determinare il formato
                // in base al numero totale di colonne, che è caratteristico per ogni fornitore
                if (headers.Length >= 35) // Wuerth ha sempre più di 35 colonne
                    return "Wuerth";
                if (headers.Length == 21) // SVAI ha sempre esattamente 21 colonne
                    return "Svai";
                if (headers.Length < 10)  // Innerhofer ha sempre meno di 10 colonne
                    return "Innerhofer";

                // Se non riusciamo a determinare il formato in nessun modo, lanciamo un'eccezione
                throw new InvalidOperationException("Impossibile determinare il formato del tracciato");
            }
            catch (Exception ex)
            {
                // Logghiamo l'errore e lo rilanciamo
                Console.WriteLine($"Errore nel determinare il formato: {ex.Message}");
                throw;
            }
        }


        // Funzione che controllava il formato tramite il nome, magari aggiungere questa come controllo ulteriore potrebbe avere senso

        //private string DeterminaFormatoTracciato(string text)
        //{
        //    if (text.Contains("INNERHOFER", StringComparison.OrdinalIgnoreCase))
        //        return "Innerhofer";
        //    if (text.Contains("WUERTH", StringComparison.OrdinalIgnoreCase))
        //        return "Wuerth";
        //    if (text.Contains("SVAI", StringComparison.OrdinalIgnoreCase))
        //        return "Svai";
        //    if (text.Contains("SPAZIO", StringComparison.OrdinalIgnoreCase))
        //        return "Spazio";

        //    throw new InvalidOperationException("Impossibile determinare il formato del tracciato");
        //}


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
            }
            else
            {
                Console.WriteLine("cliente verificato");
            }
        }



        private DocumentoToImport ReadDDT_from_Innerhofer(string text)
        {
            // Inizializza il documento con i dati fissi del fornitore
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "INNERHOFER",  //Momentanea
                FornitoreDescrizione = "Innerhofer",
                DocTipo = "DDT"
            };

            // Splitta il file in righe
            var lines = text.Split('\n');

            // Processa ogni riga saltando l'intestazione
            for (int i = 1; i < lines.Length; i++)
            {
                // Salta le righe vuote
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                // Splitta la riga nei vari campi usando il separatore ;
                var fields = lines[i].Split(';');

                // Verifica che la riga abbia tutti i campi necessari
                if (fields.Length < 9) continue;

                // Aggiorna i dati di testata solo alla prima riga valida
                if (documento.DocNumero == null)
                {
                    documento.DocNumero = fields[7].Trim();  // Numero bolla
                    documento.DocData = DateTime.Parse(fields[8].Trim());  // Data bolla
                }

                // Crea una nuova riga del DDT
                var riga = new RigaDet
                {
                    RigaNumero = i,
                    ArticoloCodiceFornitore = fields[0].Trim(),
                    ArticoloCodiceGenerico = fields[1].Trim(),
                    ArticoloDescrizione = fields[2].Trim(),
                    PrezzoUnitario = ParseImporto(fields[4]),
                    Qta = ParseImporto(fields[5]),
                    PrezzoTotale = ParseImporto(fields[6])
                };


                // Aggiunge la riga al documento
                documento.RigheDelDoc.Add(riga);
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

                    // Parsing della data con gestione formato
                    if (DateTime.TryParse(fields[columnIndexes["DATA_DDT"]].Trim(), out DateTime docData))
                    {
                        documento.DocData = docData;
                    }

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
                FornitoreDescrizione = "SVAI Srl",
                DocTipo = "DDT"
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

            var requiredColumns = new[]
            {
                "Numero_Bolla", "Data_Bolla", "Rag_Soc_1", "Indirizzo", "CAP", "Localita", "Provincia",
                "Codice_Articolo", "Descrizione_Articolo", "Quantita", "Prezzo", "Netto_Riga", "IVA"
            };

            foreach (var column in requiredColumns)
            {
                if (!columnIndexes.ContainsKey(column))
                    throw new InvalidOperationException($"Colonna richiesta mancante: {column}");
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split(';');
                if (fields.Length < headers.Length) continue;

                if (documento.DocNumero == null)
                {
                    documento.DocNumero = fields[columnIndexes["Numero_Bolla"]].Trim();

                    if (DateTime.TryParse(fields[columnIndexes["Data_Bolla"]].Trim(), out DateTime docData))
                    {
                        documento.DocData = docData;
                    }

                    var ragSoc1 = fields[columnIndexes["Rag_Soc_1"]].Trim();

                    var ragSoc2 = columnIndexes.ContainsKey("Rag_Soc_2") ?
                        fields[columnIndexes["Rag_Soc_2"]].Trim() : "";
                    documento.Cliente_AgileDesc = string.IsNullOrEmpty(ragSoc2) ?
                        ragSoc1 : $"{ragSoc1} {ragSoc2}";

                    documento.DestinazioneMerce1 = fields[columnIndexes["Indirizzo"]].Trim();
                    documento.DestinazioneMerce2 = $"{fields[columnIndexes["CAP"]].Trim()} " +
                                                 $"{fields[columnIndexes["Localita"]].Trim()} " +
                                                 $"({fields[columnIndexes["Provincia"]].Trim()})";
                }

                try
                {
                    var riga = new RigaDet
                    {
                        RigaNumero = i,
                        RigaTipo = columnIndexes.ContainsKey("Tipo_Riga") ?
                            fields[columnIndexes["Tipo_Riga"]].Trim() : "",

                        ArticoloCodiceFornitore = fields[columnIndexes["Codice_Articolo"]].Trim(),

                        ArticoloMarca = columnIndexes.ContainsKey("Marca") ?
                            fields[columnIndexes["Marca"]].Trim() : "",

                        ArticoloDescrizione = fields[columnIndexes["Descrizione_Articolo"]].Trim(),

                        ArticoloCodiceGenerico = columnIndexes.ContainsKey("Codice Fornitore") ?
                            fields[columnIndexes["Codice Fornitore"]].Trim() : "",

                        // Parsing di TUTTI i valori numerici con ParseImporto per gestire i . e ,
                        Qta = ParseImporto(fields[columnIndexes["Quantita"]]),
                        PrezzoUnitario = ParseImporto(fields[columnIndexes["Prezzo"]]),
                        PrezzoTotale = ParseImporto(fields[columnIndexes["Netto_Riga"]]),
                        PrezzoTotaleScontato = ParseImporto(fields[columnIndexes["Netto_Riga"]]),
                        IVAAliquota = ParseImporto(fields[columnIndexes["IVA"]]),

                        // Parsing di tutti gli sconti con ParseImporto per gestire i . e ,
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

        private DocumentoToImport ReadDDT_from_SPAZIO(string text)
        {
            // Implementazione placeholder per Wuerth
            var documento = new DocumentoToImport
            {
                Fornitore_AgileID = "SPAZIO",
                DocTipo = "DDT"
                // Implementare la logica di parsing specifica per Wuerth
            };

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

        public static void WriteDDTToFile(DocumentoToImport doc)
        {
            string outputPath = "output.txt";
            using (StreamWriter writer = new StreamWriter(outputPath))
            {
                // Scrivi intestazione documento
                writer.WriteLine("=== DATI DOCUMENTO ===");
                writer.WriteLine($"Fornitore: {doc.FornitoreDescrizione}");
                writer.WriteLine($"Tipo Documento: {doc.DocTipo}");
                writer.WriteLine($"Numero: {doc.DocNumero}");
                writer.WriteLine($"Data: {doc.DocData:dd/MM/yyyy}");
                writer.WriteLine($"Cliente: {doc.Cliente_AgileDesc}");
                writer.WriteLine($"Destinazione: {doc.DestinazioneMerce1}");
                writer.WriteLine($"             {doc.DestinazioneMerce2}");

                // Scrivi prima riga del documento
                if (doc.RigheDelDoc.Count > 0)
                {
                    var primaRiga = doc.RigheDelDoc[0];
                    writer.WriteLine("\n=== PRIMA RIGA ===");
                    writer.WriteLine($"Codice: {primaRiga.ArticoloCodiceFornitore}");
                    writer.WriteLine($"Descrizione: {primaRiga.ArticoloDescrizione}");
                    writer.WriteLine($"Quantità: {primaRiga.Qta}");
                    writer.WriteLine($"Prezzo Unitario: {primaRiga.PrezzoUnitario:C2}");
                    writer.WriteLine($"Prezzo Totale: {primaRiga.PrezzoTotale:C2}");
                }

                writer.WriteLine($"\nTotale righe nel documento: {doc.RigheDelDoc.Count}");
            }
        }
    } //C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Innerhofer DDT 23-24.csv  
}//C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\Wuerth CSV - DDT_8826546665_20240503_154226.csv
//C:\Users\kevin\OneDrive\Documenti\lavoro\Import DDT\SVAI ddt.csv
