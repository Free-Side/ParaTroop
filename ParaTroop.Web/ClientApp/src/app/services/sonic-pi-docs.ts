import { Injectable } from "@angular/core";

const docs = require('../../sonic-pi-docs.json');

type FunctionInfo = {
  arguments: Array<[string, string]>;
  // TODO can we get more type info for these?
  options: { [key: string]: string };
  expectedBlock: "None" | "Optional" | "Required";
}

@Injectable({ providedIn: 'root' })
export class SonicPiDocs {
  private _allSynths: string[];
  private _allFx: string[];
  private _allSamples: string[];
  private _allLang: string[];
  private _langAutocomplete: { [key: string]: FunctionInfo }
  private _synthAutocomplete: string[];
  private _fxAutocomplete: string[];
  private _sampleAutocomplete: string[];

  get allSynths(): string[] {
    return this._allSynths;
  }

  get allFx(): string[] {
    return this._allFx;
  }

  get allSamples(): string[] {
    return this._allSamples;
  }

  get allLang(): string[] {
    return this._allLang;
  }

  get synthDocumentation(): { [key: string]: string } {
    return docs.documentation.synths;
  }

  get fxDocumentation(): { [key: string]: string } {
    return docs.documentation.fx;
  }

  get samplesDocumentation(): { [key: string]: string } {
    return docs.documentation.samples;
  }

  get langDocumentation(): { [key: string]: string } {
    return docs.documentation.lang;
  }

  get langAutocomplete(): { [key: string]: FunctionInfo } {
    return this._langAutocomplete;
  }

  get fxAutocomplete(): string[] {
    return this._fxAutocomplete;
  }

  get synthAutocomplete(): string[] {
    return this._synthAutocomplete;
  }

  get sampleAutocomplete(): string[] {
    return this._sampleAutocomplete;
  }

  get fxAutocompleteOptions(): { [key: string]: string[] } {
    return docs.autocomplete.fx;
  }

  constructor() {
    this._allSynths = Object.getOwnPropertyNames(this.synthDocumentation).sort();
    this._allFx = Object.getOwnPropertyNames(this.fxDocumentation).sort();
    this._allSamples = Object.getOwnPropertyNames(this.samplesDocumentation).sort();
    this._allLang = Object.getOwnPropertyNames(this.langDocumentation).sort();
    this._langAutocomplete = {};
    for (const [key, value] of docs.autocomplete.lang) {
      this._langAutocomplete[key] = value;
    }
    this._synthAutocomplete = this._allSynths.map(v => `:${v}`);
    this._fxAutocomplete = this._allFx.map(v => `:${v}`);
    this._sampleAutocomplete = this._allSamples.map(v => `:${v}`);
  }
}
