import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { SonicPiDocs } from 'src/app/services/sonic-pi-docs';
import { MatSelectionListChange } from '@angular/material/list';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-sonic-pi-help',
  templateUrl: './sonic-pi-help.component.html',
  styleUrls: ['./sonic-pi-help.component.scss']
})
export class SonicPiHelpComponent implements OnInit {
  private _helpContainer: HTMLDivElement;
  private _helpContentFrame: HTMLIFrameElement;
  @ViewChild('helpContainer')
  protected set helpContainer(value: ElementRef) {
    this._helpContainer = value && value.nativeElement;
    if (this._helpContainer) {
      this.onHelpContainerInitialized();
    }
  }

  constructor(public documentation: SonicPiDocs) {
  }

  ngOnInit(): void {
  }

  showSelectedSynth(event: MatSelectionListChange) {
    const name = event.options[0].value;
    const content = this.documentation.synthDocumentation[name];
    this.displayHelpContent('Synth', name, content)
  }

  showSelectedFx(event: MatSelectionListChange) {
    const name = event.options[0].value;
    const content = this.documentation.fxDocumentation[name];
    this.displayHelpContent('FX', name, content)
  }

  showSelectedSamples(event: MatSelectionListChange) {
    const name = event.options[0].value;
    const content = this.documentation.samplesDocumentation[name];
    this.displayHelpContent('Samples', name, content)
  }

  showSelectedFn(event: MatSelectionListChange) {
    const name = event.options[0].value;
    const content = this.documentation.langDocumentation[name];
    this.displayHelpContent('Function', name, content)
  }

  displayHelpContent(type: string, name: string, content: string) {
    this._helpContentFrame.contentWindow.document.open();
    this._helpContentFrame.contentWindow.document.write(
      '<html lang="en-us">' +
      '<head>' +
      `<title>${type}: ${name}</title>` +
      '<link rel="stylesheet" type="text/css" href="/assets/sonic-pi-docs.css" />' +
      '</head>' +
      content +
      '</html>'
    );

    this._helpContentFrame.contentWindow.document.close();
    this._helpContentFrame.contentWindow.scrollTo(0, 0);
  }

  private onHelpContainerInitialized() {
    this._helpContentFrame = document.createElement('iframe');
    this._helpContainer.appendChild(this._helpContentFrame);
    this._helpContentFrame.contentWindow.document.open();
    this._helpContentFrame.contentWindow.document.write('<html><body><pre style="line-height: 1">' +
        '                                        ▗    \n' +
        '                                ▗        ▜▙    \n' +
        '                                 ▜▙       ▝▓▄    \n' +
        '                            ▚     ▝█▄      ▝▓█    \n' +
        '                ▂▅▅▅▅▅▅▅▆▛   ▓▖     █▓      ▀▓▌    \n' +
        '               ▝ ▐▓   ▐▓     ▐▓▌    ▐▓█      ▓▓▌    \n' +
        '                 ▓▌   ▓▌      ▓█     ▓▓      ▓▓█    \n' +
        '                ▐▓▎  ▐▓       ▓█     ▓▓      ▓▓▌    \n' +
        '                ▓▊   ▓▊      ▐▓▌    ▐▓▊     ▐▓█    \n' +
        '                ▀▘   ▀▘     ▐▓▌    ▐▓▌     ▄▓▌    \n' +
        '                           ▐▛     ▟▓▘     ▟▓▘   \n' +
        '                           ▘     ▛▘     ▗▛▘   \n' +
        '\n' +
        '             _____             __        ____  __\n' +
        '            / ___/____  ____  /_/____   / __ \\/_/\n' +
        '            \\__ \\/ __ \\/ __ \\/ / ___/  / /_/ / /\n' +
        '           ___/ / /_/ / / / / / /__   / ____/ /\n' +
        '          /____/\\____/_/ /_/_/\\___/  /_/   /_/</pre></body></html>');
    this._helpContentFrame.contentWindow.document.close();
  }
}
