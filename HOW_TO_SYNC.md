# How to Share Your Work (Setup & Sync)

You don't need to know anything about code to use this. You only ever:

1. **Double-click a file**, and
2. **Press buttons in GitHub Desktop.**

There are two files that do the hard work for you:

| File | What it does | How often |
|---|---|---|
| **Setup** | Gets your computer ready | **Once**, the very first time |
| **Sync** | Swaps work with your teammates and rebuilds the sounds | **Every time** you want to share your work or get theirs |

> **Mac:** use `Setup.command` and `Sync.command`
> **Windows:** use `Setup.bat` and `Sync.bat`

---

## How to find these files

1. Open **GitHub Desktop**.
2. In the top menu, click **Repository → Show in Finder** (Mac) or **Repository → Show in Explorer** (Windows).
3. A folder opens. `Setup` and `Sync` are right there.

---

## Part 1: First-time setup (do this once)

1. Make sure you have **GitHub Desktop** installed and the project downloaded in it.
2. In GitHub Desktop, open **Settings → Git** (on Mac: **GitHub Desktop → Settings → Git**). Type your **Name** and **Email**, then press **Save**.
3. In GitHub Desktop, check that **Current Branch** (at the top) says **`wwise-integration`**. If not, click it and pick `wwise-integration`.
4. Double-click **Setup** (`Setup.command` on Mac, `Setup.bat` on Windows).
5. A window with text opens. Let it run. It may:
   - **Ask for your Mac password.** Type it and press Return. *Nothing will appear as you type. That's normal.*
   - **Ask "Do you want to allow this app to make changes?"** on Windows. Click **Yes**.
   - Take 10–20 minutes the first time. Go get a coffee. ☕
6. When it's done you'll see one of these:
   - ✅ **`Setup complete.`** You're done! Press Return to close the window.
   - ⚠️ **`Setup finished with warnings`**. Read the yellow **WARNING** lines. Each one tells you what to fix. Fix them, then double-click Setup again.

You can run Setup as many times as you like. It's safe.

---

## Part 2: Every time you share work (4 steps)

Do this whenever you've finished something and want your teammates to have it, or when you want to get their latest work.

### Step 1: Save your work in GitHub Desktop ("Commit")

1. **Save** everything in Wwise and Unity (File → Save).
2. Open **GitHub Desktop**. On the left you'll see a list of files you changed.
3. In the bottom-left box, type a short note about what you did (for example: *"Added door creak sound"*).
4. Press the blue **Commit to wwise-integration** button.

> ⛔ **Do NOT press "Push origin" or "Pull origin" yet.** Sync handles that part.

### Step 2: Close Wwise

**Quit Wwise completely.** Sync won't run while Wwise is open.

(Unity can stay open. If it is, Sync will remind you to save in Unity, then press Return.)

### Step 3: Double-click Sync

Double-click **Sync** (`Sync.command` on Mac, `Sync.bat` on Windows).

It will:
- Download your teammates' work and combine it with yours
- Rebuild all the sounds (this can take a few minutes)

Wait until you see green **`SYNC COMPLETE`**.

### Step 4: Upload ("Push")

Go back to **GitHub Desktop** and press **Push origin** (top right).

🎉 **Done!** Your teammates can now get your work. You can open Wwise again.

> **The button says "Pull origin" instead of "Push origin"?**
> That means a teammate uploaded something while you were syncing.
> **Don't press Pull.** Just run Sync again (Step 3), then Push.

---

## The golden rules

1. ⛔ **Never press "Pull origin" in GitHub Desktop.** Always use Sync instead.
2. 💾 **Commit first, then Sync, then Push.** Always in that order.
3. 🔇 **Close Wwise before you Sync.**
4. 🌿 **Stay on the `wwise-integration` branch** (shown at the top of GitHub Desktop).

---

## If Sync stops with red text

Sync **never** deletes or breaks your committed work. If something goes wrong, it stops and tells you what to do. Find your message below:

| Sync says... | What to do |
|---|---|
| **"You have changes that are not committed yet"** | Go back to Step 1 and Commit in GitHub Desktop. Then run Sync again. |
| **"Wwise is open"** | Save and quit Wwise. Run Sync again. |
| **"You are on the branch ..."** | In GitHub Desktop, click **Current Branch** at the top and pick **`wwise-integration`**. Run Sync again. |
| **"Git does not know your name yet"** | In GitHub Desktop, open **Settings → Git**, fill in Name and Email, press **Save**. Run Sync again. |
| **"Git or Git LFS is not installed"** | Run **Setup** first (Part 1). |
| **"Wwise ... was not found"** | Install the exact Wwise version it names, using the **Audiokinetic Launcher**. |
| **"Could not reach GitHub"** | Check your internet connection. Run Sync again. |
| **"You and a teammate changed the same file(s)"** | Your work is safe. You and a teammate edited the same thing. Post in the group chat with the file names Sync listed, and agree on who keeps what. **Don't press Pull.** |
| **"Wwise could not rebuild the soundbanks"** | Open Wwise and press **Generate All**. If that works, close Wwise and run Sync again. If not, send Hubert the file Sync mentions. |
| **"An earlier merge was left unfinished"** | Don't touch anything. Message Hubert. |
| **Anything else** | Take a screenshot of the window and send it to Hubert. |

---

## Other common questions

**My Mac says the file "can't be opened" when I double-click it.**
Right-click the file, choose **Open**, then click **Open** again in the pop-up. You only have to do this once.

**A window flashes and closes on Windows.**
Run **Setup.bat** first. If it still happens, send Hubert a screenshot.

**Do I need to commit the soundbank files?**
No need to worry about them. Sync rebuilds and saves them for you.

**How do I just get my teammates' latest work without sharing anything?**
Run Sync anyway (Steps 2 and 3). If it says "Nothing to upload", you're done. No need to Push.

---

## Words you might see

| Word | What it means |
|---|---|
| **Commit** | Saving a snapshot of your work on your own computer. Nobody else sees it yet. |
| **Push** | Uploading your saved snapshots so teammates can get them. |
| **Pull** | Downloading teammates' work. *(Sync does this for you, so don't press it yourself.)* |
| **Branch** | A separate copy of the project the team works in. Ours is `wwise-integration`. |
| **Conflict** | You and a teammate changed the same thing, and the computer can't decide whose to keep. |
| **Soundbanks** | The sound files Wwise builds for the game. Sync rebuilds these automatically. |
