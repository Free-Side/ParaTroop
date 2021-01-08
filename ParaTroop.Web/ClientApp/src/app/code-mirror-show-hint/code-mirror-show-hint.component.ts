import { Component, OnInit, ViewEncapsulation } from '@angular/core';

/*
 * This component exists solely to inject ~codemirror/addon/hint/show-hint.css
 */
@Component({
  selector: 'app-code-mirror-show-hint',
  template: '',
  styleUrls: ['./code-mirror-show-hint.component.scss'],
  encapsulation: ViewEncapsulation.None
})
export class CodeMirrorShowHintComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

}
