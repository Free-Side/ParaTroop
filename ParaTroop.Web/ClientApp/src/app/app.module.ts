import { HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { FlexLayoutModule } from '@angular/flex-layout';
import { FormsModule } from '@angular/forms';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { BrowserModule } from '@angular/platform-browser';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule } from '@angular/router';
import { CodemirrorModule } from '@ctrl/ngx-codemirror';
import { AngularSplitModule } from 'angular-split';

import { AppComponent } from './app.component';
import { HomeComponent } from './home/home.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { SonicPiHelpComponent } from './sonic-pi-help/sonic-pi-help.component';
import { TroopComponent } from './troop/troop.component';
import { TroopLoginDialogComponent } from './troop-login-dialog/troop-login-dialog.component';
import { CodeMirrorShowHintComponent } from './code-mirror-show-hint/code-mirror-show-hint.component';
import { TroopListComponent } from './troop-list/troop-list.component';
import { RegisterTroopDialogComponent } from './register-troop-dialog/register-troop-dialog.component';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    HomeComponent,
    TroopComponent,
    TroopLoginDialogComponent,
    SonicPiHelpComponent,
    CodeMirrorShowHintComponent,
    TroopListComponent,
    RegisterTroopDialogComponent
  ],
  imports: [
    AngularSplitModule,
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    CodemirrorModule,
    FlexLayoutModule,
    FormsModule,
    HttpClientModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatMenuModule,
    MatToolbarModule,
    MatTableModule,
    MatTabsModule,
    NoopAnimationsModule,
    RouterModule.forRoot([
        { path: '', component: HomeComponent, pathMatch: 'full' },
        { path: 'troops', component: TroopListComponent, pathMatch: 'full' },
        { path: 'troops/:id', component: TroopComponent, pathMatch: 'full' },
      ],
      { anchorScrolling: 'enabled' }
    )
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule {
}
