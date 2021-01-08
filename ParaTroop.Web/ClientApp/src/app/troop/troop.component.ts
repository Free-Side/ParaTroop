import { Component, OnInit, ViewChild, ViewEncapsulation } from '@angular/core';
import { flatten } from '@angular/compiler';
import { HttpClient } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { CodemirrorComponent } from '@ctrl/ngx-codemirror';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Editor, EditorChange, Hint, Hints, TextMarker, Token } from 'codemirror';
import * as CodeMirror from 'codemirror';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import 'codemirror/mode/ruby/ruby';
import 'codemirror/keymap/vim';
import 'codemirror/addon/comment/comment.js';
import 'codemirror/addon/selection/active-line.js';
import 'codemirror/addon/hint/show-hint';
import 'codemirror/addon/edit/closebrackets';
import 'codemirror/addon/edit/matchbrackets';

import {
  TroopLoginDialogComponent,
  TroopLoginDialogResult
} from 'src/app/troop-login-dialog/troop-login-dialog.component';
import { Troop } from 'src/app/models/troop';
import { environment } from 'src/environments/environment';
import {
  isDelete,
  isInsert,
  isRetain,
  OperationalTransformClient,
  TextOperation
} from 'src/app/sync/operational-transform-client';
import { TroopMessage } from 'src/app/sync/troop-message';
import { MessageType } from 'src/app/sync/message-type';
import { HotkeyManager } from 'src/app/services/hotkey-manager';
import { SonicPiDocs } from 'src/app/services/sonic-pi-docs';

type SelectionRange = { start: number, end: number };

function areSelectionsEqual(sel1: SelectionRange, sel2: SelectionRange) {
  if (!sel1) {
    return !sel2;
  } else if (!sel2) {
    return false;
  } else {
    return sel1.start === sel2.start && sel1.end === sel2.end;
  }
}

type Peer = { name: string, position: number, color: string, mark: TextMarker };

function isWhitespaceToken(token: CodeMirror.Token) {
  return token.type == null && /^\s+$/.test(token.string);
}

@Component({
  selector: 'app-troop',
  templateUrl: './troop.component.html',
  styleUrls: ['./troop.component.scss']
})
export class TroopComponent extends OperationalTransformClient implements OnInit {
  public troop: Troop;
  public document: string;

  @ViewChild('textEditor')
  protected set textEditor(component: CodemirrorComponent) {
    this._textEditor = component;
    if (this._textEditor) {
      this.onTextEditorInitialized();
    }
  }


  @ViewChild('textEditorContainer')
  protected textEditorContainer: HTMLDivElement;

  // CodeMirror doesn't tell us about certain changes until after more changes have already been
  // applied to its content. So we have to maintain a separate version of the document state in
  // order to generate change operations.
  private _documentState: string = '';
  private _textEditor: CodemirrorComponent;
  private _hubConnection: HubConnection;
  private _clientId: number;
  private _nextMessageId: number = 1;
  private _peers: Map<number, Peer> = new Map<number, Peer>();

  private _onDestroy = new Subject<void>();

  private _peerColors: { [key: string]: boolean } = {
    'Aqua': false,
    'Brown': false,
    'Blue': false,
    'BlueViolet': false,
    'Chartreuse': false,
    'Coral': false,
    'Crimson': false,
    'DarkGreen': false,
    'DarkOrange': false,
    'DeepPink': false,
    'DodgerBlue': false,
    'GoldenRod': false,
    'Indigo': false,
    'Navy': false,
    'Violet': false,
  };

  constructor(
    private _router: Router,
    private _route: ActivatedRoute,
    private _httpClient: HttpClient,
    private _dialog: MatDialog,
    private _hotkeys: HotkeyManager,
    private _documentation: SonicPiDocs) {

    super();
  }

  async ngOnInit(): Promise<void> {
    const troopId = Number(this._route.snapshot.params['id']);

    this.troop = await this._httpClient.get<Troop>(`${environment.apiRoot}/troops/${troopId}`).toPromise();

    this._hubConnection = new HubConnectionBuilder().withUrl(`${environment.apiRoot}/hub`).build();
    this._hubConnection.on('Joined', this.onJoined.bind(this));
    this._hubConnection.on('Error', this.onError.bind(this));
    this._hubConnection.on('ReceiveMessage', this.onReceiveMessage.bind(this));

    // Prompt for username and password
    const loginDialog = this._dialog.open(TroopLoginDialogComponent, {
      width: '300px',
      data: { troop: this.troop }
    });

    const result = <TroopLoginDialogResult> (await loginDialog.afterClosed().toPromise());
    if (result) {
      await this._hubConnection.start();
      await this._hubConnection.invoke(
        'JoinTroop',
        {
          TroopId: this.troop.id,
          Username: result.username,
          PasswordHash: result.passwordHash
        }
      );
    } else {
      await this._router.navigate(['']);
    }

    this._hotkeys.defaults.preventDefault = true;
    this.registerHotkey('meta.enter', this.runAll);
    this.registerHotkey('control.enter', this.runAll);
    this.registerHotkey('control.r', this.runAll);
    this.registerHotkey('shift.meta.enter', this.runSelection);
    this.registerHotkey('shift.control.enter', this.runSelection);
    this.registerHotkey('meta.s', this.stop);
    this.registerHotkey('control.s', this.stop);
    // Prevent default so that the browser shortcut doesn't happen
    this.registerHotkey('control./', () => {
    });
    this.registerHotkey('control.space', this.showCompletion);
  }

  private registerHotkey(keys: string, handler: () => void) {
    this._hotkeys.addShortcut({ keys: keys })
      .pipe(takeUntil(this._onDestroy))
      .subscribe(handler.bind(this));
  }

  ngOnDestroy() {
    this._onDestroy.next();
    this._onDestroy.complete();
  }

  private onTextEditorInitialized() {
    let cm = this._textEditor.codeMirror;
    cm.setOption(
      <any> 'configureMouse',
      () => {
        return { addNew: false };
      }
    );

    cm.on('change', this.onDocumentChanged.bind(this));
    cm.on('cursorActivity', this.onSelectionChanged.bind(this));
    cm.setOption('extraKeys', {
      'Tab': function (cm) {
        let spaces = Array(cm.getOption('indentUnit') + 1).join(' ');
        let selection = cm.listSelections()[0];
        if (!selection || selection.empty()) {
          cm.replaceSelection(spaces);
        } else {
          cm.indentSelection('add');
        }
      },
      'Ctrl-/': function (cm) {
        cm.execCommand('toggleComment');
      },
      'Cmd-/': function (cm) {
        cm.execCommand('toggleComment');
      },
      'Ctrl-w': function (cm) {
        cm.execCommand('toggleComment');
      },
    });

  }

  private onJoined(clientId: number): void {
    this._clientId = clientId;
  }

  private onError(errorMessage: string): void {
    console.error(errorMessage);
  }

  private onReceiveMessage(message: TroopMessage): void {
    console.log(message);
    switch (message.type) {
      case MessageType.Connect:
        this.onReceiveConnect(message);
        break;
      case MessageType.Operation:
        this.onReceiveOperation(message);
        break;
      case MessageType.SetMark:
        this.onReceiveSetMark(message);
        break;
      case MessageType.Remove:
        this.onReceiveRemove(message);
        break;
      case MessageType.EvaluateString:
        break;
      case MessageType.EvaluateBlock:
        break;
      case MessageType.GetAll:
        break;
      case MessageType.Select:
        break;
      case MessageType.SetAll:
      case MessageType.Reset:
        this.onReceiveReset(message);
        break;
      case MessageType.Kill:
        break;
      case MessageType.RequestAck:
        this.onReceiveRequestAck(message);
        break;
      case MessageType.Console:
        break;
      case MessageType.KeepAlive:
        break;
    }
  }

  private _ignoreChanges: boolean = false;

  private onDocumentChanged(sender: Editor, change: EditorChange) {
    console.log("Document changed:");
    console.log(change);
    if (!this._ignoreChanges) {
      let cm = sender;
      let operation = new TextOperation();

      let pos = cm.indexFromPos(change.from);
      if (pos > 0) {
        operation.retain(pos);
      }

      let removedLen = (change.removed || []).join('\n').length;
      if (removedLen > 0) {
        operation.delete(removedLen);
      }

      let inserted = change.text.join('\n');
      if (inserted) {
        operation.insert(inserted);
      }

      // this._documentState has not yet received the update
      let trailing = this._documentState.length - removedLen - pos;
      if (trailing > 0) {
        operation.retain(trailing);
      }

      this.applyClientOperation(operation);
      this._documentState = operation.apply(this._documentState);

      // Forego notifying the server about cursor position changes
      let selection = sender.listSelections()[0];
      this._lastReportedMark = sender.indexFromPos(selection.head);
    } else {
      console.log('Ignoring server initiated change.');
    }
  }

  private _lastReportedMark: number = 0;
  private _lastReportedSelection: SelectionRange = { start: 0, end: 0 };

  private onSelectionChanged(sender: Editor) {
    let selection = sender.listSelections()[0];
    let markIx = sender.indexFromPos(selection.head);
    if (markIx != this._lastReportedMark) {
      this.sendMessage({
        type: MessageType.SetMark,
        index: markIx,
        reply: 0
      });

      this._lastReportedMark = markIx;
    }
    let anchorIx = sender.indexFromPos(selection.anchor);
    let newSelection: SelectionRange;
    if (anchorIx === markIx) {
      newSelection = { start: 0, end: 0 };
    } else {
      newSelection = { start: Math.min(markIx, anchorIx), end: Math.max(markIx, anchorIx) };
    }

    if (!areSelectionsEqual(this._lastReportedSelection, newSelection)) {
      this.sendMessage({
        type: MessageType.Select,
        start: newSelection.start,
        end: newSelection.end,
        reply: 0
      });

      this._lastReportedSelection = newSelection;
    }
  }

  applyOperation(operation: TextOperation): void {
    let previousDocument = this.document;

    let cm = this._textEditor.codeMirror;
    this._ignoreChanges = true;
    try {

      cm.operation(() => {
        let pos = 0;

        for (const instruction of operation.instructions) {
          if (isRetain(instruction)) {
            pos += instruction;
          } else if (isInsert(instruction)) {
            cm.replaceRange(instruction, cm.posFromIndex(pos))
            pos += instruction.length;
          } else if (isDelete(instruction)) {
            cm.replaceRange('', cm.posFromIndex(pos), cm.posFromIndex(pos - instruction));
          } else {
            throw new Error(`Unrecognized instruction type: ${instruction}`);
          }
        }
      });
    } finally {
      this._ignoreChanges = false;
    }

    this._documentState = operation.apply(this._documentState);
    if (this.document !== this._documentState) {
      console.warn(`Document Update Mismatch\nExpected: ${this._documentState}\nActual: ${this.document}`);
    }
  }

  sendOperation(operation: TextOperation): void {
    this.sendMessage({
      type: MessageType.Operation,
      operation: operation.instructions,
      revision: this.revision
    });
  }

  private sendMessage(message: TroopMessage) {
    message.messageId = this._nextMessageId++;
    message.sourceClientId = this._clientId;

    const method = `Send${MessageType[message.type]}Message`;
    console.log(`Invoking ${method} with payload: ${JSON.stringify(message)}`);
    this._hubConnection.invoke(method, message);
  }

  private onReceiveConnect(message: TroopMessage) {
    if (message.sourceClientId !== this._clientId) {
      let cm = this._textEditor.codeMirror;
      let markElement = document.createElement('div');
      markElement.className = 'peer-position';
      let color = this.allocateNextColor();
      markElement.style.backgroundColor = color;
      markElement.title = message.name;
      let bookmark =
        cm.setBookmark(cm.posFromIndex(0), { widget: markElement });
      this._peers.set(
        message.sourceClientId,
        { name: message.name, position: 0, color: color, mark: bookmark }
      );
    }
  }

  private onReceiveRemove(message: TroopMessage) {
    let peer = this._peers.get(message.sourceClientId);
    if (peer) {
      peer.mark.clear();
      peer.mark.replacedWith.remove();
      this._peers.delete(message.sourceClientId);
      this.freeColor(peer.color);
    }
  }

  private onReceiveRequestAck(message: TroopMessage) {
    if (message.flag === 1) {
      this.sendMessage({
        type: MessageType.ConnectAck,
        reply: 0
      });
    }
  }

  private onReceiveReset(message: TroopMessage) {
    this.reset();
    this._ignoreChanges = true;
    this._documentState = message.document;
    this._textEditor.codeMirror.setValue(message.document);
    this._ignoreChanges = false;

    for (let key in message.clientLocations) {
      if (message.clientLocations.hasOwnProperty(key)) {
        let position = message.clientLocations[key];
        let clientId = Number(key);

        if (clientId != this._clientId) {
          let peer = this._peers.get(clientId);
          if (peer) {
            this.updatePeerMark(peer, position)
          }
        }
      }
    }
  }

  private onReceiveOperation(message: TroopMessage) {
    if (message.sourceClientId == this._clientId) {
      // Given the lossy nature of our connection to the server we need to handle the eventuality where we never get the ACK because something was dropped. #LongTermTODO
      this.handleServerAck();
    } else if (message.operation) {
      // This is an operation from another client
      let op = new TextOperation(message.operation);
      this.applyServerOperation(op);

      // TODO: handle updates to client positions
    }
  }

  private onReceiveSetMark(message: TroopMessage) {
    if (message.sourceClientId !== this._clientId) {
      let peer = this._peers.get(message.sourceClientId);
      if (peer) {
        this.updatePeerMark(peer, message.index);
      }
    }
  }

  private allocateNextColor(): string {
    for (let key in this._peerColors) {
      if (this._peerColors.hasOwnProperty(key) && this._peerColors[key] === false) {
        this._peerColors[key] = true;
        return key;
      }
    }

    // last resort, a bad idea:
    return `#${[...Array(6).keys()].map(_ => Math.round(Math.random() * 15).toString(16)).join('')}`;
  }

  private freeColor(color: string) {
    if (this._peerColors.hasOwnProperty(color)) {
      this._peerColors[color] = false;
    }
  }

  private updatePeerMark(peer: Peer, position: number) {
    let markElement = peer.mark.replacedWith;
    peer.mark.clear();
    let cm = this._textEditor.codeMirror;
    peer.position = position;
    peer.mark = cm.setBookmark(cm.posFromIndex(position), { widget: markElement })
  }

  runAll() {
    this.sendMessage({
      type: MessageType.EvaluateBlock,
      start: 1,
      end: this._textEditor.codeMirror.lineCount(),
      reply: 1
    });
  }

  runSelection() {
    let selection = this._textEditor.codeMirror.listSelections()[0];

    this.sendMessage({
      type: MessageType.EvaluateBlock,
      start: Math.min(selection.anchor.line, selection.head.line) + 1,
      end: Math.max(selection.anchor.line, selection.head.line) + 2,
      reply: 1
    });
  }

  stop() {
    this.sendMessage({
      type: MessageType.StopAll
    });
  }

  showCompletion() {
    this._textEditor.codeMirror.showHint({
      hint: this.getCompletionOptions.bind(this)
    });
  }

  getCompletionOptions(cm: Editor): Hints {
    /* Things we could possibly be auto completing:
     *   * Generic keyword or function
     *   * A specific type of positional argument (sample, fx, or synth)
     *   * The name of an argument
     *   * An instance method
     *
     * The default/fallback case is to complete a Generic function or keyword.
     * The most difficult case to deduce is the argument name case, and to a lesser extent the
     * positional argument.
     *
     * Examples:
     *
     *     # Tokens: variable " " atom , " " atom " " number " " keyword
     *     with_fx :reverb, amp: 2 do
     *     ↑       ↑        ↑    ↑ ↑
     *     │       │        │    └─┴─ Generic function or keyword
     *     │       │        └─ Option name
     *     │       └─ Positional argument (type: fx)
     *     └─ Generic function or keyword
     *
     *     # Tokens: variable ( atom , " " atom " " number ) " " keyword
     *     with_fx(:reverb, amp: 2) do
     *
     *     with_fx :reverb, room: (rand 0.9), amp: 2 do
     *     with_fx :reverb, room: rand(0.9), amp: 2 do
     *     with_fx :reverb, room: rand, amp: 2 do
     *
     *     [1, 2, (look offset: 1)]
     *
     * Basic function forms:
     *     variable (expr comma)* (label expr comma)* (label expr)?
     *     variable open_paren (expr comma)* (label expr comma)* (label expr)? close_paren
     *
     * 1. Is the current or previous token a period?
     *     A. Is the token before the period a Number? Context: Numeric instance methods
     *     B. Is the token before the period a String? Context: String instance methods
     *     C. Is the token before the period a Regexp? Context: Regexp instance methods
     *     D. Otherwise, Context: All instance methods.
     * 1. Is the current or previous non-whitespace token a comma?
     *     A. Walk backward, counting positional arguments until:
     *         i. A variable directly precedes an atom or expression.
     *             * Is that a know function with a corresponding positional argument with a well known type? Use that for autocomplete
     *             * Is that a know function without a corresponding positional argument? Use options for autocomplete.
     *             * Otherwise, Context: Generic function or keyword
     *         ii. The beginning of the line is reached. Continue on the previous line.
     *         iii. An unmatched [ or { is found. We're inside an array or hash literal. Context: Generic keyword or function
     * 2. Is the previous non-whitespace token a variable?
     *     A. Use that variable as the Context function.
     *         i. Is that a known function with a positional argument with a well known type? Use that for autocomplete
     *         ii. Is that a know function without a position argument? Use options for autocomplete.
     *         iii. Otherwise, Context: Generic function or keyword
     */

    const cursor = cm.getCursor();
    let tokens = cm.getLineTokens(cursor.line);

    const cursorToken = cm.getTokenAt(cursor);
    const tokenToComplete = isWhitespaceToken(cursorToken) ? undefined : cursorToken;
    const cursorTokenIx =
      tokens.findIndex(t => t.start === cursorToken.start && t.end === cursorToken.end);

    let token = cursorToken;
    let tokenIx = cursorTokenIx;

    function getPreviousToken() {
      tokenIx--;
      token = tokens[tokenIx];
      return tokenIx >= 0;
    }

    // First pass, is the cursor within or just after a literal or operator?
    if (token.type === 'operator' && token.string !== ':' && token.string !== '.' ||
      // token.type === 'number' ||
      // token.type === 'string' ||
      token.type === 'string-2') {
      // Don't try to auto complete primitive types or operators
      return undefined;
    }

    // First pass, part two, the cursor is immediately after something that has special meaning
    // (a dot or semi-colon) or is at the end of an expression.
    switch (token.string) {
      case '.':
        return this.getInstanceMemberCompletionOptions(cm, cursor, tokens, tokenIx);
      case ';':
        return this.getGenericFunctionOrKeywordCompletionOptions(cursor);
      case ')':
      case ']':
        // We're at the tail end of an expression with no whitespace or delimiter, auto complete
        // makes no sense here.
        return null;
    }

    // Find the closest non-whitespace token
    while (getPreviousToken() && isWhitespaceToken(token)) {
      // Noop
    }

    if (tokenIx < 0) {
      // We're at the beginning of the line. No line wrapping because the rules are too
      // complicated/inconsistent in ruby
      return this.getGenericFunctionOrKeywordCompletionOptions(cursor, tokenToComplete);
    }

    // Second pass, the previous non-whitespace token has some special meaning
    switch (token.string) {
      case '.':
        return this.getInstanceMemberCompletionOptions(cm, cursor, tokens, tokenIx, tokenToComplete);
      case ';':
        return this.getGenericFunctionOrKeywordCompletionOptions(cursor, tokenToComplete);
    }

    // We're not at the begging of the line, or following some special token. We need to walk
    // backward looking for context.
    let functionContext = TroopComponent.getFunctionContext(tokens, cursorTokenIx);
    if (functionContext) {
      // Do we recognize this function?
      let sonicPiFn = this._documentation.langAutocomplete[functionContext.name];
      if (sonicPiFn) {
        let hints: (Hint | string)[] = undefined;
        if (functionContext.isLabel || functionContext.position >= sonicPiFn.arguments.length) {
          if (sonicPiFn.options) {
            hints = Object.getOwnPropertyNames(sonicPiFn.options).map(v => `${v}: `);
          } else {
            hints = [];
          }
          if (functionContext.name === 'with_fx' && functionContext.arguments.length > 0) {
            let fxExpression = functionContext.arguments[0];
            if (!(fxExpression instanceof Array) && fxExpression.type === 'atom') {
              let fxOptions =
                this._documentation.fxAutocompleteOptions[fxExpression.string.substr(1)];
              if (fxOptions) {
                hints.push(...fxOptions.map(str => `${str}:`))
              }
            }
          }
          // TODO add synth option autocomplete for *_synth_defaults functions
          // Unfortunately we cannot determine which synth is currently selected so we'll just have
          // to merge them all.
        } else if (functionContext.option) {
          // Special cases:
          //   "*env_curve": 1=linear, 2=exponential, 3=sine, 4=welch, 6=squared, 7=cubed
          if (functionContext.option.endsWith('env_curve:')) {
            hints = [
              { displayText: '1 - linear', text: '1' },
              { displayText: '2 - exponential', text: '2' },
              { displayText: '3 - sine', text: '3' },
              { displayText: '4 - welch', text: '4' },
              { displayText: '6 - squared', text: '6' },
              { displayText: '7 - cubed', text: '7' },
            ]
          }
        } else if (functionContext.position >= 0) {
          let argument = sonicPiFn.arguments[functionContext.position];
          if (argument) {
            // Special cases:
            //  * tonic (note symbol)
            //  * note (note symbol)
            //  * fundamental_note (note symbol)
            //  * chord - name (chord type)
            //  * scale (scale name)
            //  * scale - name (scale name)
            //  * tuning (:equal, :just, :pythagorean, :meantone)
            //  * noise_type (:white, :pink, :light_pink, :dark_pink :perlin)
            //  * degree (:i, :ii, :iii, :iv, :v, :vi, :vii, 'a(1-7'), 'd(1-7)', 'aa(1-7)', 'dd(1-7)', 'p(1-7)')
            //  * sample - name_or_path (sample)
            //  * synth_name (synth)
            //  * fx_name (fx)
            switch (argument[0]) {
              case 'note':
              case 'fundamental_note':
              case 'tonic':
                // Complete note symbol
                hints = ArgumentOptions.note;
                break;
              case 'chord':
                // Complete chord type
                hints = ArgumentOptions.chord;
                break;
              case 'scale':
                // Complete scale name
                hints = ArgumentOptions.scale;
                break;
              case 'tuning':
                // Complete tuning
                hints = ArgumentOptions.tuning;
                break;
              case 'noise_type':
                // Complete noise type
                hints = ArgumentOptions.noise_type;
                break;
              case 'degree':
                // Complete degree
                hints = ArgumentOptions.degree;
                break;
              case 'sample':
                // Complete sample
                hints = this._documentation.sampleAutocomplete;
                break;
              case 'synth_name':
                // Complete synth
                hints = this._documentation.synthAutocomplete;
                break;
              case 'fx_name':
                // Complete fx
                hints = this._documentation.fxAutocomplete;
                break;
              case 'name':
                if (functionContext.name === 'chord') {
                  hints = ArgumentOptions.chord;
                } else if (functionContext.name === 'scale') {
                  hints = ArgumentOptions.scale;
                }

                break;
            }
          }
        }

        if (hints) {
          if (tokenToComplete) {
            hints =
              hints.filter(v => {
                let str = typeof v === 'string' ? v : v.text;
                return str.startsWith(tokenToComplete.string) ||
                  str.startsWith(`:${tokenToComplete.string}`) ||
                  // Special cases for completing string chord definitions like "7-11"
                  str.startsWith(`"${tokenToComplete.string}`) ||
                  tokenToComplete.type === 'string' &&
                    str.startsWith(tokenToComplete.string.substr(0, tokenToComplete.string.length - 1));
              })
          }

          console.log(hints);

          return {
            list: hints,
            from: tokenToComplete ? CodeMirror.Pos(cursor.line, tokenToComplete.start) : cursor,
            to: tokenToComplete ? CodeMirror.Pos(cursor.line, tokenToComplete.end) : cursor
          };
        }
      }
    }

    if (token.type === 'operator' && token.string !== ':' ||
      token.type === 'number' ||
      token.type === 'string' ||
      token.type === 'string-2') {
      // Don't try to auto complete primitive types or operators
      return undefined;
    }
    // Fallback
    return this.getGenericFunctionOrKeywordCompletionOptions(cursor, tokenToComplete);
  }

  private getInstanceMemberCompletionOptions(
    cm: Editor,
    cursor: CodeMirror.Position,
    tokens: Token[],
    tokenIx: number,
    cursorToken?: Token): Hints {

    let token: Token;
    function getPreviousToken() {
      tokenIx--;
      token = tokens[tokenIx];
      return tokenIx >= 0;
    }

    getPreviousToken();
    while (tokenIx > 0 && isWhitespaceToken(token) && getPreviousToken()) {
      // Skip whitespace
    }

    let hints: string[] = undefined;

    if (tokenIx >= 0) {
      switch (token.type) {
        case 'number':
          hints = RubyFunctions.Integer;
          break;
        case 'string':
          hints = RubyFunctions.String;
          break;
        case 'string-2':
          hints = RubyFunctions.Regexp;
          break;
        case null:
        case undefined:
          if (token.string === ']') {
            hints = RubyFunctions.SonicPiArray.concat(RubyFunctions.Array);
          } else if (token.string === '}') {
            hints = RubyFunctions.Object;
          }
      }
    }

    if (!hints) {
      // Don't know what is before the dot, try everything.
      hints = RubyFunctions.Object
        .concat(RubyFunctions.String)
        .concat(RubyFunctions.Integer)
        .concat(RubyFunctions.Regexp)
        .concat(RubyFunctions.Array)
        .concat(RubyFunctions.SonicPiArray)
    }

    if (cursorToken) {
      hints = hints.filter(h => h.startsWith(cursorToken.string));
      hints.sort();

      // Replace the current token
      return {
        from: CodeMirror.Pos(cursor.line, cursorToken.start),
        to: CodeMirror.Pos(cursor.line, cursorToken.end),
        list: hints
      }
    } else {
      return {
        from: cursor,
        to: cursor,
        list: hints
      }
    }
  }

  private getGenericFunctionOrKeywordCompletionOptions(
    cursor: CodeMirror.Position,
    cursorToken?: Token) {
    if (cursorToken) {
      // Replace the current token
      return {
        from: CodeMirror.Pos(cursor.line, cursorToken.start),
        to: CodeMirror.Pos(cursor.line, cursorToken.end),
        list:
          this._documentation.allLang
            .concat((<any> CodeMirror).hintWords.ruby)
            .filter(fn => fn.startsWith(cursorToken.string))
      }
    } else {
      return {
        from: cursor,
        to: cursor,
        list: this._documentation.allLang.concat((<any> CodeMirror).hintWords.ruby)
      }
    }
  }

  private static getFunctionContext(tokens: Token[], tokenIx: number): MethodContext {
    let position = 0;
    let fnArguments: Array<Token | Token[]> = [];
    let option: string = undefined;
    let isLabel = false;
    let isFirstToken = true;
    let currentExpression: Token | Token[] = undefined;

    let token = tokens[tokenIx];
    let elementType = TroopComponent.classifyToken(token);

    function getPreviousToken() {
      tokenIx--;
      token = tokens[tokenIx];
      elementType = TroopComponent.classifyToken(token);
      return tokenIx >= 0;
    }

    let expecting = MethodElement.Any;

    while (true) {
      if (elementType !== MethodElement.Whitespace) {
        if ((elementType & expecting) !== elementType) {
          return undefined;
        }

        switch (elementType) {
          case MethodElement.Variable:
            if ((MethodElement.Expression & expecting) === MethodElement.Expression) {
              // Variables count as expressions too
              expecting = MethodElement.VariableLabelCommaOrOpenParen;
              currentExpression = token;
            } else {
              if (currentExpression) {
                fnArguments.unshift(currentExpression);
              }

              return {
                name: token.string,
                position: option ? -1 : position,
                arguments: fnArguments,
                option: option,
                isLabel: isLabel
              }
            }

            break;
          case MethodElement.OpenParen:
            if (getPreviousToken() && elementType === MethodElement.Variable) {
              if (currentExpression) {
                fnArguments.unshift(currentExpression);
              }

              return {
                name: token.string,
                position: option ? -1 : position,
                arguments: fnArguments,
                option: option,
                isLabel: isLabel
              }
            } else {
              return undefined;
            }
          case MethodElement.Expression:
            if ((token.type === null || token.type === undefined) &&
              /^[)}\]]$/.test(token.string)) {
              // This expression ends with a group terminating token, consume the expression.

              let braces = [token.string];
              let checkForMethod = false;
              let expressionTokens: Token[] = [token];

              while (braces.length > 0 && getPreviousToken()) {
                expressionTokens.unshift(token);
                if (token.type === null || token.type === undefined) {
                  switch (token.string) {
                    case '(': {
                      let brace = braces.pop();
                      if (brace === ')') {
                        if (braces.length === 0) {
                          // If the immediately preceding token is a variable then that is also part
                          // of the expression (i.e. functions might look like (max 1, 2) or
                          // max(1, 2))
                          checkForMethod = true;
                        }
                      } else {
                        // Brace mismatch
                        return undefined;
                      }
                      break;
                    }
                    case '[': {
                      let brace = braces.pop();
                      if (brace !== ']') {
                        // Brace mismatch
                        return undefined;
                      }
                      break;
                    }
                    case '{': {
                      break;
                    }
                    case ')':
                    case ']':
                    case '}':
                      braces.push(token.string);
                      break;
                  }
                }
              }

              if (checkForMethod && tokenIx > 0 && tokens[tokenIx - 1].type === 'variable') {
                expressionTokens.unshift(tokens[tokenIx - 1]);
                // Consume the preceding variable as well
                tokenIx--;
              }

              currentExpression = expressionTokens;
            } else {
              currentExpression = token;
              // Otherwise this is assumed to be a single token expression. Note: there is certainly
              // syntax that ruby supports that this does not
            }

            expecting = MethodElement.VariableLabelCommaOrOpenParen;
            break;
          case MethodElement.Comma:
            if (!option) {
              position++;
            }
            if (currentExpression) {
              fnArguments.unshift(currentExpression);
              currentExpression = undefined;
            }

            expecting = MethodElement.VariableOrExpression;
            break;
          case MethodElement.Label:
            if (!option) {
              option = token.string;

              if (position > 0) {
                // We've previously encountered a , but not yet encountered a label, that means that
                // whatever we were completing after that comma was probably the start of another label.
                isLabel = true;
              }

              // No longer relevant.
              position = -1;
            }

            currentExpression = undefined;

            expecting = MethodElement.VariableCommaOrOpenParen;
            break;
        }
      } else if (isFirstToken) {
        // If the first token is whitespace we can rule out the preceding token being an expression
        expecting = MethodElement.VariableLabelCommaOrOpenParen;
      }

      isFirstToken = false;
      if (!getPreviousToken()) {
        return undefined;
      }
    }
  }

  private static classifyToken(token: Token): MethodElement {
    if (!token) {
      return MethodElement.Unknown;
    }

    switch (token.type) {
      case 'variable':
        return MethodElement.Variable;
      case 'atom':
        return token.string.length > 1 && token.string.endsWith(':') ?
          MethodElement.Label :
          MethodElement.Expression;
      case 'operator':
        if (token.string === ':') {
          // Special case for the beginning of an atom :foo
          return MethodElement.Expression;
        }

        // Otherwise we hit an operator, all hope is lost
        return MethodElement.Unknown;
      case null:
      case undefined:
        if (token.string === ',') {
          return MethodElement.Comma;
        } else if (token.string === '(') {
          // Special case for finding a method name before an open paren (i.e. `foo(here`)
          return MethodElement.OpenParen;
        } else if (/^\s+$/.test(token.string)) {
          return MethodElement.Whitespace;
        } else if (/^[\[{]$/.test(token.string)) {
          // If we walk back into an unmatched [ or { that is not good
          return MethodElement.Unknown;
        } else {
          // Anything else is an expression
          return MethodElement.Expression;
        }
      default:
        return MethodElement.Expression;
    }
  }
}

enum MethodElement {
  Unknown = 0,
  Variable = 1,
  Expression = 2,
  Label = 4, // An atom ending with a :
  Comma = 8,
  OpenParen = 16,
  Whitespace = 32,
  VariableOrExpression = Variable | Expression, // Things that may precede a comma
  VariableLabelCommaOrOpenParen = Variable | Label | Comma | OpenParen, // Things that precede an expression
  VariableCommaOrOpenParen = Variable | Comma | OpenParen, // Things that precede a label
  Any = Variable | Expression | Label | Comma | OpenParen // Initial state
}

type MethodContext = {
  name: string; // The name of the method for which we are completing an argument
  arguments: Array<Token | Token[]>; // The list of positional arguments
  position: number; // The position of the current token, if applicable
  option?: string; // The label preceding the current token, if any
  isLabel?: boolean; // True if we are definitely completing a label (for example: `foo bar, baz: 1, here`)
}

const RubyFunctions = {
  Integer: [
    'times',
    'abs',
    'allbits?',
    'anybits?',
    'bit_length',
    'ceil',
    'chr',
    'coerce',
    'denominator',
    'digits',
    'div',
    'divmod',
    'downto',
    'even?',
    'fdiv',
    'floor',
    'gcd',
    'gcdlcm',
    'inspect',
    'integer?',
    'lcm',
    'magnitude',
    'modulo',
    'next',
    'nobits?',
    'numerator',
    'odd?',
    'ord',
    'pow',
    'pred',
    'rationalize',
    'remainder',
    'round',
    'size',
    'succ',
    'to_f',
    'to_i',
    'to_int',
    'to_r',
    'to_s',
    'truncate',
    'upto'],
  String: [
    'bytes',
    'bytesize',
    'byteslice',
    'capitalize',
    'capitalize!',
    'casecmp',
    'casecmp?',
    'center',
    'chars',
    'chomp',
    'chomp!',
    'chop',
    'chop!',
    'chr',
    'clear',
    'codepoints',
    'concat',
    'count',
    'crypt',
    'delete',
    'delete!',
    'delete_prefix',
    'delete_prefix!',
    'delete_suffix',
    'delete_suffix!',
    'downcase',
    'downcase!',
    'dump',
    'each_byte',
    'each_char',
    'each_codepoint',
    'each_line',
    'empty?',
    'encode',
    'encode!',
    'encoding',
    'end_with?',
    'eql?',
    'freeze',
    'gsub',
    'gsub!',
    'hash',
    'hex',
    'include?',
    'index',
    'insert',
    'inspect',
    'intern',
    'length',
    'lines',
    'ljust',
    'lstrip',
    'lstrip!',
    'match',
    'match?',
    'next',
    'next!',
    'oct',
    'ord',
    'partition',
    'prepend',
    'replace',
    'reverse',
    'reverse!',
    'rindex',
    'rjust',
    'rpartition',
    'rstrip',
    'rstrip!',
    'scan',
    'size',
    'slice',
    'slice!',
    'split',
    'squeeze',
    'squeeze!',
    'start_with?',
    'strip',
    'strip!',
    'sub',
    'sub!',
    'succ',
    'succ!',
    'sum',
    'swapcase',
    'swapcase!',
    'to_c',
    'to_f',
    'to_i',
    'to_r',
    'to_s',
    'to_str',
    'to_sym',
    'tr',
    'tr!',
    'tr_s',
    'tr_s!',
    'upcase',
    'upcase!',
    'upto'],
  Regexp: [
    'match',
    'match?',
    'named_captures',
    'names',
    'options',
    'source'
  ],
  Array: [
    'any?',
    'append',
    'assoc',
    'at',
    'bsearch',
    'bsearch_index',
    'clear',
    'collect',
    'collect!',
    'combination',
    'compact',
    'compact!',
    'concat',
    'count',
    'cycle',
    'delete',
    'delete_at',
    'delete_if',
    'dig',
    'drop',
    'drop_while',
    'each',
    'each_index',
    'empty?',
    'eql?',
    'fetch',
    'fill',
    'find_index',
    'first',
    'flatten',
    'flatten!',
    'frozen?',
    'hash',
    'include?',
    'index',
    'initialize_copy',
    'insert',
    'inspect',
    'join',
    'keep_if',
    'last',
    'length',
    'map',
    'map!',
    'max',
    'min',
    'pack',
    'permutation',
    'pop',
    'prepend',
    'product',
    'push',
    'rassoc',
    'reject',
    'reject!',
    'repeated_combination',
    'repeated_permutation',
    'replace',
    'reverse',
    'reverse!',
    'reverse_each',
    'rindex',
    'rotate',
    'rotate!',
    'sample',
    'select',
    'select!',
    'shift',
    'shuffle',
    'shuffle!',
    'size',
    'slice',
    'slice!',
    'sort',
    'sort!',
    'sort_by!',
    'sum',
    'take',
    'take_while',
    'to_a',
    'to_ary',
    'to_h',
    'to_s',
    'transpose',
    'uniq',
    'uniq!',
    'unshift',
    'values_at',
    'zip'],
  Object: [
    'clone',
    'display',
    'dup',
    'enum_for',
    'eql?',
    'extend',
    'freeze',
    'frozen?',
    'inspect',
    'instance_of?',
    'is_a?',
    'itself',
    'kind_of?',
    'method',
    'methods',
    'nil?',
    'object_id',
    'respond_to?',
    'send',
    'taint',
    'tainted?',
    'tap',
    'to_enum',
    'to_s',
    'untaint',
    'yield_self'
  ],
  SonicPiArray: [
    'choose',
    'pick'
  ]
}

const ArgumentOptions = {
  note: [ ':ab', ':a', ':as', ':bb', ':b', ':c', ':cs', ':db', ':d', ':ds', ':eb', ':e', ':f', ':fs', ':gb', ':g', ':gs' ].sort(),
  chord: [
    '"5"',
    '"+5"',
    '"m+5"',
    ':sus2',
    ':sus4',
    '"6"',
    ':m6',
    '"7sus2"',
    '"7sus4"',
    '"7-5"',
    ':halfdiminished',
    '"7+5"',
    '"m7+5"',
    '"9"',
    ':m9',
    '"m7+9"',
    ':maj9',
    '"9sus4"',
    '"6*9"',
    '"m6*9"',
    '"7-9"',
    '"m7-9"',
    '"7-10"',
    '"7-11"',
    '"7-13"',
    '"9+5"',
    '"m9+5"',
    '"7+5-9"',
    '"m7+5-9"',
    '"11"',
    ':m11',
    ':maj11',
    '"11+"',
    '"m11+"',
    '"13"',
    ':m13',
    ':add2',
    ':add4',
    ':add9',
    ':add11',
    ':add13',
    ':madd2',
    ':madd4',
    ':madd9',
    ':madd11',
    ':madd13',
    ':major',
    ':maj',
    ':M',
    ':minor',
    ':min',
    ':m',
    ':major7',
    ':dom7',
    '"7"',
    ':M7',
    ':minor7',
    ':m7',
    ':augmented',
    ':a',
    ':diminished',
    ':dim',
    ':i',
    ':diminished7',
    ':dim7',
    ':i7',
    ':halfdim',
    '"m7b5"',
    '"m7-5"' ].sort(),
  scale: [
    ':diatonic',
    ':ionian',
    ':major',
    ':dorian',
    ':phrygian',
    ':lydian',
    ':mixolydian',
    ':aeolian',
    ':minor',
    ':locrian',
    ':hex_major6',
    ':hex_dorian',
    ':hex_phrygian',
    ':hex_major7',
    ':hex_sus',
    ':hex_aeolian',
    ':minor_pentatonic',
    ':yu',
    ':major_pentatonic',
    ':gong',
    ':egyptian',
    ':shang',
    ':jiao',
    ':zhi',
    ':ritusen',
    ':whole_tone',
    ':whole',
    ':chromatic',
    ':harmonic_minor',
    ':melodic_minor_asc',
    ':hungarian_minor',
    ':octatonic',
    ':messiaen1',
    ':messiaen2',
    ':messiaen3',
    ':messiaen4',
    ':messiaen5',
    ':messiaen6',
    ':messiaen7',
    ':super_locrian',
    ':hirajoshi',
    ':kumoi',
    ':neapolitan_major',
    ':bartok',
    ':bhairav',
    ':locrian_major',
    ':ahirbhairav',
    ':enigmatic',
    ':neapolitan_minor',
    ':pelog',
    ':augmented2',
    ':scriabin',
    ':harmonic_major',
    ':melodic_minor_desc',
    ':romanian_minor',
    ':hindu',
    ':iwato',
    ':melodic_minor',
    ':diminished2',
    ':marva',
    ':melodic_major',
    ':indian',
    ':spanish',
    ':prometheus',
    ':diminished',
    ':todi',
    ':leading_whole',
    ':augmented',
    ':purvi',
    ':chinese',
    ':lydian_minor',
    ':blues_major',
    ':blues_minor' ].sort(),
  tuning: [ ':equal', ':just', ':meantone', ':pythagorean' ],
  noise_type: [':white', ':pink', ':light_pink', ':dark_pink', ':perlin'],
  degree: [':i', ':ii', ':iii', ':iv', ':v', ':vi', ':vii']
    .concat(flatten(
      Array(7)
        .map((_, ix) => ix + 1)
        .map(v => [`a${v}`, `aa${v}`, `d${v}`, `dd${v}`, `p${v}`])))
};
