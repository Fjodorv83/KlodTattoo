# Deploy su Railway.app - KlodTattoo Web

Questa guida ti aiuterà a fare il deploy gratuito di KlodTattoo Web su Railway.app con database PostgreSQL.

## Prerequisiti

- Account GitHub
- Account Railway.app (gratuito - registrati su [railway.app](https://railway.app))
- Progetto su GitHub

## Caratteristiche del Deploy

- **Database**: PostgreSQL (fornito gratuitamente da Railway)
- **Hosting**: Railway.app (piano gratuito disponibile)
- **SSL**: Automatico
- **Deploy**: Automatico ad ogni push su GitHub
- **Migrazioni**: Applicate automaticamente all'avvio

## Passo 1: Prepara il Repository

1. Assicurati che il codice sia su GitHub:
```bash
git add .
git commit -m "Preparazione per deploy su Railway"
git push origin main
```

## Passo 2: Crea Progetto su Railway

1. Vai su [railway.app](https://railway.app)
2. Clicca su **"Start a New Project"**
3. Seleziona **"Deploy from GitHub repo"**
4. Autorizza Railway ad accedere al tuo GitHub
5. Seleziona il repository **KlodTattooWeb**

## Passo 3: Aggiungi Database PostgreSQL

1. Nel tuo progetto Railway, clicca su **"+ New"**
2. Seleziona **"Database"**
3. Scegli **"Add PostgreSQL"**
4. Railway creerà automaticamente il database e fornirà la variabile `DATABASE_URL`

## Passo 4: Configura le Variabili d'Ambiente

1. Clicca sul servizio della tua applicazione (non il database)
2. Vai alla tab **"Variables"**
3. Aggiungi le seguenti variabili:

```
ASPNETCORE_ENVIRONMENT=Production
ADMIN_EMAIL=admin@klodtattoo.com
ADMIN_PASSWORD=TuaPasswordSicura123!
```

### Variabili Opzionali (Email):
```
EmailSettings__SmtpServer=smtp.gmail.com
EmailSettings__SmtpPort=587
EmailSettings__SmtpUsername=tua_email@gmail.com
EmailSettings__SmtpPassword=tua_app_password
EmailSettings__SenderEmail=noreply@klodtattoo.com
EmailSettings__SenderName=KlodTattoo Web
```

**IMPORTANTE**: Railway fornisce automaticamente le variabili `DATABASE_URL` e `PORT` - non devi aggiungerle manualmente!

## Passo 5: Collega il Database all'Applicazione

1. Nella dashboard del progetto, clicca sul servizio PostgreSQL
2. Vai alla tab **"Connect"**
3. Copia la variabile di connessione (dovrebbe essere già disponibile come `DATABASE_URL`)
4. Railway collegherà automaticamente il database all'applicazione

## Passo 6: Deploy

Railway inizierà automaticamente il deploy:

1. Builderà l'immagine Docker
2. Applicherà le migrazioni del database
3. Creerà l'utente admin con le credenziali fornite
4. Avvierà l'applicazione

## Passo 7: Verifica il Deploy

1. Nella dashboard Railway, clicca sul servizio dell'applicazione
2. Vai alla tab **"Deployments"**
3. Attendi che lo stato diventi **"Active"**
4. Clicca su **"View Logs"** per vedere i log in tempo reale
5. Una volta completato, clicca sul link del dominio per aprire l'applicazione

Railway ti fornirà un URL tipo: `https://klodtattooweb-production.up.railway.app`

## Accesso Admin

Dopo il primo deploy, potrai accedere come admin usando:
- **Email**: Il valore di `ADMIN_EMAIL` (default: admin@klodtattoo.com)
- **Password**: Il valore di `ADMIN_PASSWORD`

## Deploy Automatici

Railway effettuerà automaticamente un nuovo deploy ogni volta che fai push su GitHub:

```bash
git add .
git commit -m "Aggiornamento"
git push origin main
```

## Gestione Database

### Visualizza il Database

1. Clicca sul servizio PostgreSQL
2. Vai alla tab **"Data"** per vedere le tabelle
3. Oppure usa un client PostgreSQL con le credenziali fornite

### Backup

Railway effettua backup automatici. Per backup manuali:
1. Vai alla tab **"Data"** del database
2. Clicca su **"Export"**

## Monitoraggio

### Log dell'Applicazione
```
Dashboard → Servizio → Logs
```

### Metriche
```
Dashboard → Servizio → Metrics
```

## Risoluzione Problemi

### L'applicazione non si avvia

1. Verifica i log: `Dashboard → Servizio → Logs`
2. Controlla che `DATABASE_URL` sia presente nelle variabili
3. Verifica che `ADMIN_PASSWORD` rispetti i requisiti (maiuscole, minuscole, numeri, caratteri speciali)

### Errori di Migrazione

1. Vai ai log e cerca errori di migrazione
2. Se necessario, puoi eliminare e ricreare il database PostgreSQL
3. Railway riapplicherà le migrazioni automaticamente

### Database non si connette

1. Verifica che il servizio PostgreSQL sia attivo
2. Controlla che `DATABASE_URL` sia configurato
3. Verifica nei log che la connessione sia stabilita

## Dominio Personalizzato

Railway fornisce un dominio gratuito, ma puoi aggiungere il tuo:

1. Vai alla tab **"Settings"** del servizio
2. Clicca su **"Generate Domain"** per un dominio Railway gratuito
3. Oppure clicca su **"Custom Domain"** per usare il tuo dominio

## Limiti Piano Gratuito Railway

- **$5 di credito gratuito al mese**
- **500 ore di esecuzione**
- **100 GB di traffico in uscita**
- **1 GB di RAM per servizio**

Perfetto per progetti piccoli e prototipi!

## Aggiornamento dell'Applicazione

Per aggiornare l'applicazione:

```bash
# Fai le tue modifiche
git add .
git commit -m "Descrizione modifiche"
git push origin main
```

Railway effettuerà automaticamente il deploy della nuova versione.

## Database Locale vs Produzione

L'applicazione è configurata per usare:
- **SQLite** in sviluppo locale
- **PostgreSQL** in produzione (Railway)

Questo permette di sviluppare localmente senza dover configurare PostgreSQL.

## Sicurezza

1. **Cambia sempre la password admin** dopo il primo accesso
2. **Non condividere le variabili d'ambiente** pubblicamente
3. **Usa password forti** per l'account admin
4. **Abilita 2FA** sul tuo account Railway

## Supporto

- [Documentazione Railway](https://docs.railway.app)
- [Community Railway](https://discord.gg/railway)
- [Railway Status](https://status.railway.app)

## Alternative Gratuite

Se Railway non soddisfa le tue esigenze, considera:
- **Render.com** (75GB bandwidth/mese gratuito)
- **Fly.io** (3 VM gratuite)
- **Heroku** (con limitazioni dopo la rimozione del piano gratuito)

---

Buon deploy! 🚀
