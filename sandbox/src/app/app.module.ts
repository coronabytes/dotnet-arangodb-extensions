import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppComponent } from './app.component';
import {DxDataGridModule} from 'devextreme-angular';

@NgModule({
  declarations: [
    AppComponent
  ],
    imports: [
        BrowserModule,
        DxDataGridModule
    ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
